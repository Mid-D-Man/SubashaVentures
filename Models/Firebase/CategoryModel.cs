namespace SubashaVentures.Models.Firebase;

public record CategoryModel
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
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
