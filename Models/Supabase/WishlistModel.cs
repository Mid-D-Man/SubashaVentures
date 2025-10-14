namespace SubashaVentures.Models.Supabase;

public record WishlistModel : ISecureEntity
{
    public string Id { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string ProductId { get; init; } = string.Empty;
    
    // ISecureEntity
    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime? DeletedAt { get; init; }
    public string? DeletedBy { get; init; }
}
