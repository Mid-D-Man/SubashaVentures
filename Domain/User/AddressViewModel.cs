// Domain/User/AddressViewModel.cs - UPDATED FOR JSONB
using SubashaVentures.Models.Supabase;

namespace SubashaVentures.Domain.User;

public class AddressViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = "Lagos";
    public string State { get; set; } = "Lagos";
    public string PostalCode { get; set; } = "100001";
    public string Country { get; set; } = "Nigeria";
    public bool IsDefault { get; set; }
    public AddressType Type { get; set; } = AddressType.Shipping;
    public DateTime AddedAt { get; set; }

    // Display properties
    public string FullAddress => $"{AddressLine1}, {(!string.IsNullOrEmpty(AddressLine2) ? AddressLine2 + ", " : "")}{City}, {State} {PostalCode}, {Country}";
    public string ShortAddress => $"{AddressLine1}, {City}";
    public string DisplayType => Type.ToString();

    // ==================== CONVERSION METHODS ====================

    /// <summary>
    /// Convert from Supabase AddressItem to AddressViewModel
    /// </summary>
    public static AddressViewModel FromAddressItem(AddressItem item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        // Parse AddressType from string
        AddressType addressType = AddressType.Shipping;
        if (Enum.TryParse<AddressType>(item.Type, true, out var parsedType))
        {
            addressType = parsedType;
        }

        return new AddressViewModel
        {
            Id = item.Id,
            FullName = item.FullName,
            PhoneNumber = item.PhoneNumber,
            AddressLine1 = item.AddressLine1,
            AddressLine2 = item.AddressLine2,
            City = item.City,
            State = item.State,
            PostalCode = item.PostalCode,
            Country = item.Country,
            IsDefault = item.IsDefault,
            Type = addressType,
            AddedAt = item.AddedAt,
            Email = item.Email
        };
    }

    /// <summary>
    /// Convert from AddressViewModel to Supabase AddressItem
    /// </summary>
    public AddressItem ToAddressItem()
    {
        return new AddressItem
        {
            Id = this.Id,
            FullName = this.FullName,
            PhoneNumber = this.PhoneNumber,
            AddressLine1 = this.AddressLine1,
            AddressLine2 = this.AddressLine2,
            City = this.City,
            State = this.State,
            PostalCode = this.PostalCode,
            Country = this.Country,
            IsDefault = this.IsDefault,
            Type = this.Type.ToString(),
            AddedAt = this.AddedAt,
            Email = this.Email
        };
    }

    /// <summary>
    /// Convert list of AddressItems to list of AddressViewModels
    /// </summary>
    public static List<AddressViewModel> FromAddressItems(IEnumerable<AddressItem> items)
    {
        if (items == null)
            return new List<AddressViewModel>();

        return items.Select(FromAddressItem).ToList();
    }

    /// <summary>
    /// Convert from AddressModel (with JSONB items) to list of AddressViewModels
    /// </summary>
    public static List<AddressViewModel> FromAddressModel(AddressModel model)
    {
        if (model == null || model.Items == null)
            return new List<AddressViewModel>();

        return FromAddressItems(model.Items);
    }
}

public enum AddressType
{
    Shipping,
    Billing,
    Both
}