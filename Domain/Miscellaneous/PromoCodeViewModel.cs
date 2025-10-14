

// ===== Domain/Miscellaneous/PromoCodeViewModel.cs =====
namespace SubashaVentures.Domain.Miscellaneous;

public class PromoCodeViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal MinimumOrderAmount { get; set; }
    public decimal? MaximumDiscount { get; set; }
    public int? UsageLimit { get; set; }
    public int UsageCount { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool IsActive { get; set; }
    
    public bool IsValid => IsActive && 
        (ValidFrom == null || DateTime.UtcNow >= ValidFrom) &&
        (ValidUntil == null || DateTime.UtcNow <= ValidUntil) &&
        (UsageLimit == null || UsageCount < UsageLimit);
    
    public decimal CalculateDiscount(decimal orderAmount)
    {
        if (!IsValid || orderAmount < MinimumOrderAmount) return 0;
        
        var discount = DiscountType == DiscountType.Percentage
            ? orderAmount * (DiscountValue / 100)
            : DiscountValue;
        
        if (MaximumDiscount.HasValue && discount > MaximumDiscount.Value)
            discount = MaximumDiscount.Value;
        
        return discount;
    }
}

public enum DiscountType
{
    Percentage,
    FixedAmount
}
