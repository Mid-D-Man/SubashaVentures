namespace SubashaVentures.Models.Firebase;

using SubashaVentures.Domain.Enums;

public class CategoryModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public SvgType IconSvgType { get; set; } = SvgType.None;
    public int ProductCount { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
