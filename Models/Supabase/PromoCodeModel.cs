namespace SubashaVentures.Models.Supabase;

public record PromoCodeModel : ISecureEntity
{
    public string Id { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string DiscountType { get; init; } = "Percentage"; // Percentage, FixedAmount
    public decimal DiscountValue { get; init; }
    public decimal MinimumOrderAmount { get; init; }
    public decimal? MaximumDiscount { get; init; }
    public int? UsageLimit { get; init; }
    public int UsageCount { get; init; }
    public DateTime? ValidFrom { get; init; }
    public DateTime? ValidUntil { get; init; }
    public bool IsActive { get; init; }
    
    // ISecureEntity
    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime? DeletedAt { get; init; }
    public string? DeletedBy { get; init; }
}
