// Services/Addresses/IAddressService.cs - COMPLETE INTERFACE
using SubashaVentures.Domain.User;

namespace SubashaVentures.Services.Addresses;

/// <summary>
/// Service for managing user addresses
/// </summary>
public interface IAddressService
{
    /// <summary>
    /// Get all addresses for a user
    /// </summary>
    Task<List<AddressViewModel>> GetUserAddressesAsync(string userId);
    
    /// <summary>
    /// Get specific address by ID
    /// </summary>
    Task<AddressViewModel?> GetAddressByIdAsync(string userId, string addressId);
    
    /// <summary>
    /// Add new address for user
    /// </summary>
    Task<bool> AddAddressAsync(string userId, AddressViewModel address);
    
    /// <summary>
    /// Update existing address
    /// </summary>
    Task<bool> UpdateAddressAsync(string userId, AddressViewModel address);
    
    /// <summary>
    /// Delete address
    /// </summary>
    Task<bool> DeleteAddressAsync(string userId, string addressId);
    
    /// <summary>
    /// Set default address for user
    /// </summary>
    Task<bool> SetDefaultAddressAsync(string userId, string addressId);
    
    /// <summary>
    /// Get user's default address
    /// </summary>
    Task<AddressViewModel?> GetDefaultAddressAsync(string userId);
    
    /// <summary>
    /// Get count of addresses for user
    /// </summary>
    Task<int> GetAddressCountAsync(string userId);
    
    /// <summary>
    /// Validate address fields
    /// </summary>
    Task<AddressValidationResult> ValidateAddress(AddressViewModel address);
}
