// Services/Addresses/AddressService.cs - UPDATED FOR JSONB
using SubashaVentures.Domain.User;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Client = Supabase.Client;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Addresses;

public class AddressService : IAddressService
{
    private readonly Client _supabaseClient;
    private readonly ILogger<AddressService> _logger;
    private Dictionary<string, int> _addressCountCache = new();

    public AddressService(
        Client supabaseClient,
        ILogger<AddressService> logger)
    {
        _supabaseClient = supabaseClient;
        _logger = logger;
    }

    public async Task<List<AddressViewModel>> GetUserAddressesAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("GetUserAddresses called with empty userId");
                return new List<AddressViewModel>();
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"📍 Fetching addresses for user: {userId}",
                LogLevel.Info
            );

            var addresses = await _supabaseClient
                .From<AddressModel>()
                .Where(a => a.UserId == userId)
                .Single();

            if (addresses == null || !addresses.Items.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No addresses found, returning empty",
                    LogLevel.Info
                );
                return new List<AddressViewModel>();
            }

            // Update cache
            _addressCountCache[userId] = addresses.Items.Count;

            // Use conversion method
            var viewModels = AddressViewModel.FromAddressModel(addresses);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Retrieved {viewModels.Count} addresses",
                LogLevel.Info
            );

            return viewModels;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting user addresses");
            _logger.LogError(ex, "Failed to get addresses for user: {UserId}", userId);
            return new List<AddressViewModel>();
        }
    }

    public async Task<AddressViewModel?> GetAddressByIdAsync(string userId, string addressId)
    {
        try
        {
            var addresses = await GetUserAddressesAsync(userId);
            return addresses.FirstOrDefault(a => a.Id == addressId);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting address: {addressId}");
            return null;
        }
    }

    public async Task<AddressViewModel?> GetDefaultAddressAsync(string userId)
    {
        try
        {
            var addresses = await GetUserAddressesAsync(userId);
            return addresses.FirstOrDefault(a => a.IsDefault);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting default address");
            return null;
        }
    }

    public async Task<bool> AddAddressAsync(string userId, AddressViewModel address)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("AddAddress called with empty userId");
                return false;
            }

            // Validate address
            var validation = ValidateAddress(address);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Address validation failed: {Errors}", 
                    string.Join(", ", validation.Errors.Values));
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"➕ Adding address for user: {userId}",
                LogLevel.Info
            );

            // Generate new ID if not provided
            if (string.IsNullOrEmpty(address.Id))
            {
                address.Id = Guid.NewGuid().ToString();
            }

            // Set added timestamp
            address.AddedAt = DateTime.UtcNow;

            // Call Postgres function
            var result = await _supabaseClient.Rpc<List<AddressItem>>(
                "add_address",
                new
                {
                    p_user_id = userId,
                    p_address_id = address.Id,
                    p_full_name = address.FullName,
                    p_phone_number = address.PhoneNumber,
                    p_address_line1 = address.AddressLine1,
                    p_address_line2 = address.AddressLine2,
                    p_city = address.City,
                    p_state = address.State,
                    p_postal_code = address.PostalCode,
                    p_country = address.Country,
                    p_is_default = address.IsDefault,
                    p_type = address.Type.ToString()
                }
            );

            if (result != null)
            {
                // Clear cache
                _addressCountCache.Remove(userId);

                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ Successfully added address! Total: {result.Count}",
                    LogLevel.Info
                );

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Adding address");
            _logger.LogError(ex, "Failed to add address for user: {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> UpdateAddressAsync(string userId, AddressViewModel address)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(address.Id))
            {
                _logger.LogWarning("UpdateAddress called with empty userId or addressId");
                return false;
            }

            // Validate address
            var validation = ValidateAddress(address);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Address validation failed: {Errors}", 
                    string.Join(", ", validation.Errors.Values));
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"🔄 Updating address: {address.Id}",
                LogLevel.Info
            );

            // Call Postgres function
            var result = await _supabaseClient.Rpc<List<AddressItem>>(
                "update_address",
                new
                {
                    p_user_id = userId,
                    p_address_id = address.Id,
                    p_full_name = address.FullName,
                    p_phone_number = address.PhoneNumber,
                    p_address_line1 = address.AddressLine1,
                    p_address_line2 = address.AddressLine2,
                    p_city = address.City,
                    p_state = address.State,
                    p_postal_code = address.PostalCode,
                    p_country = address.Country,
                    p_is_default = address.IsDefault,
                    p_type = address.Type.ToString()
                }
            );

            if (result != null)
            {
                // Clear cache
                _addressCountCache.Remove(userId);

                await MID_HelperFunctions.DebugMessageAsync(
                    "✅ Successfully updated address",
                    LogLevel.Info
                );

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Updating address");
            _logger.LogError(ex, "Failed to update address: {AddressId}", address.Id);
            return false;
        }
    }

    public async Task<bool> DeleteAddressAsync(string userId, string addressId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(addressId))
            {
                _logger.LogWarning("DeleteAddress called with empty parameters");
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"➖ Deleting address: {addressId}",
                LogLevel.Info
            );

            // Call Postgres function
            var result = await _supabaseClient.Rpc<List<AddressItem>>(
                "remove_address",
                new 
                { 
                    p_user_id = userId,
                    p_address_id = addressId 
                }
            );

            if (result != null)
            {
                // Clear cache
                _addressCountCache.Remove(userId);

                await MID_HelperFunctions.DebugMessageAsync(
                    "✅ Successfully deleted address",
                    LogLevel.Info
                );

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Deleting address");
            _logger.LogError(ex, "Failed to delete address: {AddressId}", addressId);
            return false;
        }
    }

    public async Task<bool> SetDefaultAddressAsync(string userId, string addressId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(addressId))
            {
                _logger.LogWarning("SetDefaultAddress called with empty parameters");
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"⭐ Setting default address: {addressId}",
                LogLevel.Info
            );

            // Call Postgres function
            var result = await _supabaseClient.Rpc<List<AddressItem>>(
                "set_default_address",
                new 
                { 
                    p_user_id = userId,
                    p_address_id = addressId 
                }
            );

            if (result != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✅ Successfully set default address",
                    LogLevel.Info
                );

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Setting default address");
            _logger.LogError(ex, "Failed to set default address: {AddressId}", addressId);
            return false;
        }
    }

    public async Task<int> GetAddressCountAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return 0;
            }

            // Check cache first
            if (_addressCountCache.TryGetValue(userId, out var cachedCount))
            {
                return cachedCount;
            }

            var addresses = await _supabaseClient
                .From<AddressModel>()
                .Where(a => a.UserId == userId)
                .Single();

            var count = addresses?.Items.Count ?? 0;

            // Update cache
            _addressCountCache[userId] = count;

            return count;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting address count");
            return 0;
        }
    }

    public AddressValidationResult ValidateAddress(AddressViewModel address)
    {
        var result = new AddressValidationResult { IsValid = true };

        if (string.IsNullOrWhiteSpace(address.FullName))
            result.AddError("FullName", "Full name is required");

        if (string.IsNullOrWhiteSpace(address.PhoneNumber))
            result.AddError("PhoneNumber", "Phone number is required");
        else if (address.PhoneNumber.Length < 10)
            result.AddError("PhoneNumber", "Phone number must be at least 10 digits");

        if (string.IsNullOrWhiteSpace(address.AddressLine1))
            result.AddError("AddressLine1", "Address line 1 is required");

        if (string.IsNullOrWhiteSpace(address.City))
            result.AddError("City", "City is required");

        if (string.IsNullOrWhiteSpace(address.State))
            result.AddError("State", "State is required");

        if (string.IsNullOrWhiteSpace(address.PostalCode))
            result.AddError("PostalCode", "Postal code is required");

        if (string.IsNullOrWhiteSpace(address.Country))
            result.AddError("Country", "Country is required");

        return result;
    }
}