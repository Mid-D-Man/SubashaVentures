namespace SubashaVentures.Models.Firebase;

using SubashaVentures.Domain.Enums;

public class SubCategoryModel
{
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// ID of the parent CategoryModel this belongs to.
    /// </summary>
    public string CategoryId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public SvgType IconSvgType { get; set; } = SvgType.None;

    /// <summary>
    /// Exactly one subcategory per parent category must have this true.
    /// </summary>
    public bool IsDefault { get; set; } = false;

    public int ProductCount { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
