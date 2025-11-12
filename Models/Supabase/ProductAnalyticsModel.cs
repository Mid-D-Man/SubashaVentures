namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Individual analytics events (unlimited writes in Supabase)
/// </summary>
public record ProductAnalyticsModel : ISecureEntity
{
    public string Id { get; init; } = string.Empty;
    public string ProductId { get; init; } = string.Empty;
    public string? UserId { get; init; }
    public string? SessionId { get; init; }
    
    // Event tracking
    public string EventType { get; init; } = string.Empty; // View, Click, AddToCart, Purchase, Like
    public string? Source { get; init; } // HomePage, CategoryPage, Search, Recommendation
    public string? Device { get; init; } // Mobile, Desktop, Tablet
    public string? Location { get; init; } // City, State
    
    // Purchase-specific
    public int? Quantity { get; init; }
    public decimal? Amount { get; init; }
    
    // Metadata
    public string? UserAgent { get; init; }
    public string? IpAddress { get; init; }
    
    // ISecureEntity
    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime? DeletedAt { get; init; }
    public string? DeletedBy { get; init; }
}
