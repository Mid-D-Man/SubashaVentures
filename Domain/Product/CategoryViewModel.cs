namespace SubashaVentures.Domain.Product;

using SubashaVentures.Models.Firebase;
using SubashaVentures.Domain.Enums;

public class CategoryViewModel
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

    /// <summary>
    /// Populated by GetCategoriesWithSubcategoriesAsync.
    /// Default subcategory is always first in this list.
    /// </summary>
    public List<SubCategoryViewModel> SubCategories { get; set; } = new();

    public static CategoryViewModel FromCloudModel(CategoryModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        return new CategoryViewModel
        {
            Id = model.Id,
            Name = model.Name,
            Slug = model.Slug,
            Description = model.Description,
            ImageUrl = model.ImageUrl,
            IconSvgType = model.IconSvgType,
            ProductCount = model.ProductCount,
            DisplayOrder = model.DisplayOrder,
            IsActive = model.IsActive,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
            SubCategories = new List<SubCategoryViewModel>()
        };
    }

    public CategoryModel ToCloudModel()
    {
        return new CategoryModel
        {
            Id = this.Id,
            Name = this.Name,
            Slug = this.Slug,
            Description = this.Description,
            ImageUrl = this.ImageUrl,
            IconSvgType = this.IconSvgType,
            ProductCount = this.ProductCount,
            DisplayOrder = this.DisplayOrder,
            IsActive = this.IsActive,
            CreatedAt = this.CreatedAt,
            UpdatedAt = this.UpdatedAt
        };
    }

    public static List<CategoryViewModel> FromCloudModels(IEnumerable<CategoryModel> models)
    {
        if (models == null) return new List<CategoryViewModel>();
        return models.Select(FromCloudModel).ToList();
    }
}
