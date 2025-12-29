// ===== Domain/Miscellaneous/PromoCodeViewModel.cs =====
namespace SubashaVentures.Domain.Miscellaneous;

using SubashaVentures.Models.Supabase;

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
    
    // ==================== CONVERSION METHODS ====================
    
    /// <summary>
    /// Convert from Supabase PromoCodeModel to PromoCodeViewModel
    /// </summary>
    public static PromoCodeViewModel FromCloudModel(PromoCodeModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
            
        // Parse DiscountType from string
        DiscountType discountType = DiscountType.Percentage;
        if (Enum.TryParse<DiscountType>(model.DiscountType, true, out var parsedType))
        {
            discountType = parsedType;
        }
            
        return new PromoCodeViewModel
        {
            Id = model.Id,
            Code = model.Code,
            Description = model.Description,
            DiscountType = discountType,
            DiscountValue = model.DiscountValue,
            MinimumOrderAmount = model.MinimumOrderAmount,
            MaximumDiscount = model.MaximumDiscount,
            UsageLimit = model.UsageLimit,
            UsageCount = model.UsageCount,
            ValidFrom = model.ValidFrom,
            ValidUntil = model.ValidUntil,
            IsActive = model.IsActive
        };
    }
    
    /// <summary>
    /// Convert from PromoCodeViewModel to Supabase PromoCodeModel
    /// </summary>
    public PromoCodeModel ToCloudModel()
    {
        return new PromoCodeModel
        {
            Id = this.Id,
            Code = this.Code,
            Description = this.Description,
            DiscountType = this.DiscountType.ToString(),
            DiscountValue = this.DiscountValue,
            MinimumOrderAmount = this.MinimumOrderAmount,
            MaximumDiscount = this.MaximumDiscount,
            UsageLimit = this.UsageLimit,
            UsageCount = this.UsageCount,
            ValidFrom = this.ValidFrom,
            ValidUntil = this.ValidUntil,
            IsActive = this.IsActive,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };
    }
    
    /// <summary>
    /// Convert list of PromoCodeModels to list of PromoCodeViewModels
    /// </summary>
    public static List<PromoCodeViewModel> FromCloudModels(IEnumerable<PromoCodeModel> models)
    {
        if (models == null)
            return new List<PromoCodeViewModel>();
            
        return models.Select(FromCloudModel).ToList();
    }
}

public enum DiscountType
{
    Percentage,
    FixedAmount
}
