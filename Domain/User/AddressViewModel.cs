// Domain/User/AddressViewModel.cs - UPDATED WITH EMAIL
using SubashaVentures.Models.Supabase;

namespace SubashaVentures.Domain.User;

public class AddressViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional email for this address
    /// If not provided, will use user's primary email
    /// </summary>
    public string? Email { get; set; }
    
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = "Nigeria";
    public bool IsDefault { get; set; }
    public AddressType Type { get; set; } = AddressType.Shipping;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    
    // Display properties
    public string FullAddress => $"{AddressLine1}, {City}, {State} {PostalCode}, {Country}";
    public string ShortAddress => $"{City}, {State}";
    
    // Conversion methods
    public AddressItem ToAddressItem()
    {
        return new AddressItem
        {
            Id = this.Id,
            FullName = this.FullName,
            PhoneNumber = this.PhoneNumber,
            Email = this.Email,
            AddressLine1 = this.AddressLine1,
            AddressLine2 = this.AddressLine2,
            City = this.City,
            State = this.State,
            PostalCode = this.PostalCode,
            Country = this.Country,
            IsDefault = this.IsDefault,
            Type = this.Type.ToString(),
            AddedAt = this.AddedAt
        };
    }
    
    public static AddressViewModel FromAddressItem(AddressItem item)
    {
        return new AddressViewModel
        {
            Id = item.Id,
            FullName = item.FullName,
            PhoneNumber = item.PhoneNumber,
            Email = item.Email,
            AddressLine1 = item.AddressLine1,
            AddressLine2 = item.AddressLine2,
            City = item.City,
            State = item.State,
            PostalCode = item.PostalCode,
            Country = item.Country,
            IsDefault = item.IsDefault,
            Type = Enum.TryParse<AddressType>(item.Type, out var type) ? type : AddressType.Shipping,
            AddedAt = item.AddedAt
        };
    }
}

public enum AddressType
{
    Shipping,
    Billing,
    Both
}
