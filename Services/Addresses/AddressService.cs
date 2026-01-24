// Services/Addresses/AddressService.cs - UPDATED WITH EMAIL SUPPORT
using SubashaVentures.Domain.User;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.Users;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Postgrest;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Addresses;

public class AddressService : IAddressService
{
    private readonly ISupabaseDatabaseService _database;
    private readonly IUserService _userService;
    private readonly ILogger<AddressService> _logger;

    public AddressService(
        ISupabaseDatabaseService database,
        IUserService userService,
        ILogger<AddressService> logger)
    {
        _database = database;
        _userService = userService;
        _logger = logger;
    }

    public async Task<List<AddressViewModel>> GetUserAddressesAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new List<AddressViewModel>();
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Getting addresses for user: {userId}",
                LogLevel.Info
            );

            // Get user email for addresses
            var userEmail = await GetUserEmailAsync(userId);

            // Get address model (one row per user with JSONB items)
            var addressModels = await _database.GetWithFilterAsync<AddressModel>(
                "user_id",
                Constants.Operator.Equals,
                userId
            );

            var addressModel = addressModels.FirstOrDefault();

            if (addressModel == null || addressModel.Items == null || !addressModel.Items.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No addresses found for user",
                    LogLevel.Info
                );
                return new List<AddressViewModel>();
            }

            // Convert JSONB items to ViewModels and populate email if missing
            var addresses = new List<AddressViewModel>();
            foreach (var item in addressModel.Items)
            {
                var viewModel = AddressViewModel.FromAddressItem(item);
                
                // Populate email if not already set
                if (string.IsNullOrEmpty(viewModel.Email))
                {
                    viewModel.Email = userEmail;
                }
                
                addresses.Add(viewModel);
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Found {addresses.Count} addresses",
                LogLevel.Info
            );

            return addresses.OrderByDescending(a => a.IsDefault).ToList();
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
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(addressId))
            {
                return null;
            }

            var addresses = await GetUserAddressesAsync(userId);
            return addresses.FirstOrDefault(a => a.Id == addressId);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting address: {addressId}");
            _logger.LogError(ex, "Failed to get address: {AddressId}", addressId);
            return null;
        }
    }

    public async Task<bool> AddAddressAsync(string userId, AddressViewModel address)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Adding address for user: {userId}",
                LogLevel.Info
            );

            // Get user email if not provided
            if (string.IsNullOrEmpty(address.Email))
            {
                address.Email = await GetUserEmailAsync(userId);
            }

            // Get existing address model or create new one
            var addressModels = await _database.GetWithFilterAsync<AddressModel>(
                "user_id",
                Constants.Operator.Equals,
                userId
            );

            var addressModel = addressModels.FirstOrDefault();

            if (addressModel == null)
            {
                // Create new address model
                addressModel = new AddressModel
                {
                    UserId = userId,
                    Items = new List<AddressItem>(),
                    CreatedAt = DateTime.UtcNow
                };
            }

            // Generate new ID for address
            address.Id = Guid.NewGuid().ToString();
            address.AddedAt = DateTime.UtcNow;

            // If this is the first address, make it default
            if (!addressModel.Items.Any())
            {
                address.IsDefault = true;
            }
            else if (address.IsDefault)
            {
                // Unset other default addresses
                foreach (var item in addressModel.Items)
                {
                    item.IsDefault = false;
                }
            }

            // Add new address item
            addressModel.Items.Add(address.ToAddressItem());
            addressModel.UpdatedAt = DateTime.UtcNow;

            // Save to database
            IPostgrestTable<AddressModel>? result;
            if (addressModels.Any())
            {
                result = await _database.UpdateAsync(addressModel);
            }
            else
            {
                result = await _database.InsertAsync(addressModel);
            }

            var success = result != null && result.Any();

            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Address added successfully: {address.Id}",
                    LogLevel.Info
                );
            }

            return success;
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
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Updating address: {address.Id}",
                LogLevel.Info
            );

            // Get user email if not provided
            if (string.IsNullOrEmpty(address.Email))
            {
                address.Email = await GetUserEmailAsync(userId);
            }

            // Get address model
            var addressModels = await _database.GetWithFilterAsync<AddressModel>(
                "user_id",
                Constants.Operator.Equals,
                userId
            );

            var addressModel = addressModels.FirstOrDefault();

            if (addressModel == null || addressModel.Items == null)
            {
                return false;
            }

            // Find and update the address item
            var existingIndex = addressModel.Items.FindIndex(a => a.Id == address.Id);
            if (existingIndex == -1)
            {
                return false;
            }

            // If setting as default, unset other defaults
            if (address.IsDefault)
            {
                foreach (var item in addressModel.Items)
                {
                    item.IsDefault = false;
                }
            }

            // Update the address
            addressModel.Items[existingIndex] = address.ToAddressItem();
            addressModel.UpdatedAt = DateTime.UtcNow;

            // Save to database
            var result = await _database.UpdateAsync(addressModel);

            var success = result != null && result.Any();

            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Address updated successfully: {address.Id}",
                    LogLevel.Info
                );
            }

            return success;
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
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Deleting address: {addressId}",
                LogLevel.Info
            );

            // Get address model
            var addressModels = await _database.GetWithFilterAsync<AddressModel>(
                "user_id",
                Constants.Operator.Equals,
                userId
            );

            var addressModel = addressModels.FirstOrDefault();

            if (addressModel == null || addressModel.Items == null)
            {
                return false;
            }

            // Remove the address item
            var wasDefault = addressModel.Items.Any(a => a.Id == addressId && a.IsDefault);
            addressModel.Items.RemoveAll(a => a.Id == addressId);

            // If deleted address was default, set first remaining address as default
            if (wasDefault && addressModel.Items.Any())
            {
                addressModel.Items[0].IsDefault = true;
            }

            addressModel.UpdatedAt = DateTime.UtcNow;

            // Save to database
            var result = await _database.UpdateAsync(addressModel);

            var success = result != null && result.Any();

            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Address deleted successfully: {addressId}",
                    LogLevel.Info
                );
            }

            return success;
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
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Setting default address: {addressId}",
                LogLevel.Info
            );

            // Get address model
            var addressModels = await _database.GetWithFilterAsync<AddressModel>(
                "user_id",
                Constants.Operator.Equals,
                userId
            );

            var addressModel = addressModels.FirstOrDefault();

            if (addressModel == null || addressModel.Items == null)
            {
                return false;
            }

            // Update default flags
            foreach (var item in addressModel.Items)
            {
                item.IsDefault = (item.Id == addressId);
            }

            addressModel.UpdatedAt = DateTime.UtcNow;

            // Save to database
            var result = await _database.UpdateAsync(addressModel);

            var success = result != null && result.Any();

            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Default address set: {addressId}",
                    LogLevel.Info
                );
            }

            return success;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Setting default address");
            _logger.LogError(ex, "Failed to set default address: {AddressId}", addressId);
            return false;
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
            _logger.LogError(ex, "Failed to get default address for user: {UserId}", userId);
            return null;
        }
    }

    // ==================== PRIVATE HELPERS ====================

    private async Task<string> GetUserEmailAsync(string userId)
    {
        try
        {
            var user = await _userService.GetUserByIdAsync(userId);
            return user?.Email ?? string.Empty;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting user email for address");
            return string.Empty;
        }
    }
}
