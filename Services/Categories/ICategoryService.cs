using SubashaVentures.Domain.Product;
using SubashaVentures.Domain.Enums;

namespace SubashaVentures.Services.Categories;

public interface ICategoryService
{
    Task<List<CategoryViewModel>> GetAllCategoriesAsync();
    Task<CategoryViewModel?> GetCategoryByIdAsync(string categoryId);
    Task<CategoryViewModel?> GetCategoryBySlugAsync(string slug);
    Task<List<CategoryViewModel>> GetTopLevelCategoriesAsync();
    Task<List<CategoryViewModel>> GetSubCategoriesAsync(string parentId);
    Task<string> CreateCategoryAsync(CreateCategoryRequest request);
    Task<bool> UpdateCategoryAsync(string categoryId, UpdateCategoryRequest request);
    Task<bool> DeleteCategoryAsync(string categoryId);
    Task<int> GetProductCountForCategoryAsync(string categoryId);
}

// DTOs
public class CreateCategoryRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public SvgType IconSvgType { get; set; } = SvgType.None;
    public string? ParentId { get; set; }
    public int DisplayOrder { get; set; }
}

public class UpdateCategoryRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public SvgType? IconSvgType { get; set; }
    public int? DisplayOrder { get; set; }
    public bool? IsActive { get; set; }
}
