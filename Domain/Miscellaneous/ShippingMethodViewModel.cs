namespace SubashaVentures.Domain.Miscellaneous;

public class ShippingMethodViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public int EstimatedDays { get; set; }
    public string CourierName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    
    public string DisplayCost => Cost == 0 ? "FREE" : $"₦{Cost:N0}";
    public string DisplayEstimate => EstimatedDays == 1 
        ? "1 day" 
        : $"{EstimatedDays} days";
    
    // ==================== CONVERSION METHODS ====================
    // Note: ShippingMethodViewModel typically comes from configuration/settings
    // rather than a direct database model
}
