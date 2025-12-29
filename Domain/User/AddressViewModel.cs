namespace SubashaVentures.Domain.User;

using SubashaVentures.Models.Supabase;

public class AddressViewModel
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = "Nigeria";
    public bool IsDefault { get; set; }
    public AddressType Type { get; set; } = AddressType.Shipping;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    public string FullAddress => $"{AddressLine1}, {(!string.IsNullOrEmpty(AddressLine2) ? AddressLine2 + ", " : "")}{City}, {State} {PostalCode}, {Country}";
    
    // ==================== CONVERSION METHODS ====================
    
    /// <summary>
    /// Convert from Supabase AddressModel to AddressViewModel
    /// </summary>
    public static AddressViewModel FromCloudModel(AddressModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
            
        // Parse AddressType from string
        AddressType addressType = AddressType.Shipping;
        if (Enum.TryParse<AddressType>(model.Type, true, out var parsedType))
        {
            addressType = parsedType;
        }
            
        return new AddressViewModel
        {
            Id = model.Id,
            UserId = model.UserId,
            FullName = model.FullName,
            PhoneNumber = model.PhoneNumber,
            AddressLine1 = model.AddressLine1,
            AddressLine2 = model.AddressLine2,
            City = model.City,
            State = model.State,
            PostalCode = model.PostalCode,
            Country = model.Country,
            IsDefault = model.IsDefault,
            Type = addressType,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
    }
    
    /// <summary>
    /// Convert from AddressViewModel to Supabase AddressModel
    /// </summary>
    public AddressModel ToCloudModel()
    {
        return new AddressModel
        {
            Id = this.Id,
            UserId = this.UserId,
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
            CreatedAt = this.CreatedAt,
            CreatedBy = this.UserId,
            UpdatedAt = this.UpdatedAt
        };
    }
    
    /// <summary>
    /// Convert list of AddressModels to list of AddressViewModels
    /// </summary>
    public static List<AddressViewModel> FromCloudModels(IEnumerable<AddressModel> models)
    {
        if (models == null)
            return new List<AddressViewModel>();
            
        return models.Select(FromCloudModel).ToList();
    }
}

public enum AddressType
{
    Shipping,
    Billing,
    Both
}
