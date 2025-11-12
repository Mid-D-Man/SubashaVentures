namespace SubashaVentures.Models.Supabase;

public record CategoryModel : ISecureEntity
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ImageUrl { get; init; }
    public string? IconEmoji { get; init; }
    public string? ParentId { get; init; }
    public int ProductCount { get; init; }
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; } = true;
    
    // ISecureEntity
    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime? DeletedAt { get; init; }
    public string? DeletedBy { get; init; }
}
