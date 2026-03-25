// Models/Firebase/CategoryModel.cs
using SubashaVentures.Domain.Enums;

namespace SubashaVentures.Models.Firebase;

public class CategoryModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }

    /// <summary>
    /// SVG icon type — used on both main and sub categories
    /// </summary>
    public SvgType IconSvgType { get; set; } = SvgType.None;

    /// <summary>
    /// Null = main category. Non-null = subcategory belonging to that parent.
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// Only meaningful on subcategories.
    /// Exactly one subcategory per parent should have this set to true.
    /// </summary>
    public bool IsDefault { get; set; } = false;

    public int ProductCount { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
