namespace SubashaVentures.Domain.Product;

using SubashaVentures.Models.Firebase;
using SubashaVentures.Domain.Enums;

public class SubCategoryViewModel
{
    public string Id { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public SvgType IconSvgType { get; set; } = SvgType.None;
    public bool IsDefault { get; set; } = false;
    public int ProductCount { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public static SubCategoryViewModel FromCloudModel(SubCategoryModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        return new SubCategoryViewModel
        {
            Id = model.Id,
            CategoryId = model.CategoryId,
            Name = model.Name,
            Slug = model.Slug,
            Description = model.Description,
            ImageUrl = model.ImageUrl,
            IconSvgType = model.IconSvgType,
            IsDefault = model.IsDefault,
            ProductCount = model.ProductCount,
            DisplayOrder = model.DisplayOrder,
            IsActive = model.IsActive,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
    }

    public SubCategoryModel ToCloudModel()
    {
        return new SubCategoryModel
        {
            Id = this.Id,
            CategoryId = this.CategoryId,
            Name = this.Name,
            Slug = this.Slug,
            Description = this.Description,
            ImageUrl = this.ImageUrl,
            IconSvgType = this.IconSvgType,
            IsDefault = this.IsDefault,
            ProductCount = this.ProductCount,
            DisplayOrder = this.DisplayOrder,
            IsActive = this.IsActive,
            CreatedAt = this.CreatedAt,
            UpdatedAt = this.UpdatedAt
        };
    }

    public static List<SubCategoryViewModel> FromCloudModels(IEnumerable<SubCategoryModel> models)
    {
        if (models == null) return new List<SubCategoryViewModel>();
        return models.Select(FromCloudModel).ToList();
    }
}
