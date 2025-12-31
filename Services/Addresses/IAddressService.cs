// Services/Addresses/IAddressService.cs
using SubashaVentures.Domain.User;
using SubashaVentures.Models.Supabase;

namespace SubashaVentures.Services.Addresses;

public interface IAddressService
{
    /// <summary>
    /// Get all addresses for a user
    /// </summary>
    Task<List<AddressViewModel>> GetUserAddressesAsync(string userId);

    /// <summary>
    /// Get a specific address by ID
    /// </summary>
    Task<AddressViewModel?> GetAddressByIdAsync(string userId, string addressId);

    /// <summary>
    /// Get the default address for a user
    /// </summary>
    Task<AddressViewModel?> GetDefaultAddressAsync(string userId);

    /// <summary>
    /// Add a new address
    /// </summary>
    Task<bool> AddAddressAsync(string userId, AddressViewModel address);

    /// <summary>
    /// Update an existing address
    /// </summary>
    Task<bool> UpdateAddressAsync(string userId, AddressViewModel address);

    /// <summary>
    /// Delete an address
    /// </summary>
    Task<bool> DeleteAddressAsync(string userId, string addressId);

    /// <summary>
    /// Set an address as default
    /// </summary>
    Task<bool> SetDefaultAddressAsync(string userId, string addressId);

    /// <summary>
    /// Get address count for user
    /// </summary>
    Task<int> GetAddressCountAsync(string userId);

    /// <summary>
    /// Validate address data
    /// </summary>
    AddressValidationResult ValidateAddress(AddressViewModel address);
}

public class AddressValidationResult
{
    public bool IsValid { get; set; }
    public Dictionary<string, string> Errors { get; set; } = new();

    public void AddError(string field, string message)
    {
        Errors[field] = message;
        IsValid = false;
    }
}
