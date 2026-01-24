// Domain/Miscellaneous/ShippingMethodViewModel.cs - UPDATED
namespace SubashaVentures.Domain.Miscellaneous;

public class ShippingMethodViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public string EstimatedDays { get; set; } = string.Empty;
    public string Icon { get; set; } = "📦";
    public bool IsAvailable { get; set; } = true;
    public bool IsFree => Cost == 0;
    
    public string DisplayCost => Cost == 0 ? "FREE" : $"₦{Cost:N0}";
    public string DisplayName => $"{Icon} {Name}";
}
