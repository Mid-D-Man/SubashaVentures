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
    
    /// <summary>
    /// SVG icon type for this category (replaces IconEmoji)
    /// </summary>
    public SvgType IconSvgType { get; set; } = SvgType.None;
    
    public string? ParentId { get; set; }
    public int ProductCount { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public List<CategoryViewModel> SubCategories { get; set; } = new();
    
    // ==================== CONVERSION METHODS ====================
    
    /// <summary>
    /// Convert from Firebase CategoryModel to CategoryViewModel
    /// </summary>
    public static CategoryViewModel FromCloudModel(CategoryModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
            
        return new CategoryViewModel
        {
            Id = model.Id,
            Name = model.Name,
            Slug = model.Slug,
            Description = model.Description,
            ImageUrl = model.ImageUrl,
            IconSvgType = model.IconSvgType,
            ParentId = model.ParentId,
            ProductCount = model.ProductCount,
            DisplayOrder = model.DisplayOrder,
            IsActive = model.IsActive,
            SubCategories = new List<CategoryViewModel>()
        };
    }
    
    /// <summary>
    /// Convert from CategoryViewModel to Firebase CategoryModel
    /// </summary>
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
            ParentId = this.ParentId,
            ProductCount = this.ProductCount,
            DisplayOrder = this.DisplayOrder,
            IsActive = this.IsActive,
            CreatedAt = DateTime.UtcNow
        };
    }
    
    /// <summary>
    /// Convert list of CategoryModels to list of CategoryViewModels
    /// </summary>
    public static List<CategoryViewModel> FromCloudModels(IEnumerable<CategoryModel> models)
    {
        if (models == null)
            return new List<CategoryViewModel>();
            
        return models.Select(FromCloudModel).ToList();
    }
}
