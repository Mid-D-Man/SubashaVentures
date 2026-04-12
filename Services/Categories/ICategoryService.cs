// Services/Categories/ICategoryService.cs
namespace SubashaVentures.Services.Categories;

using SubashaVentures.Domain.Product;
using SubashaVentures.Domain.Enums;

public interface ICategoryService
{
    // ==================== CATEGORIES ====================

    /// <summary>Returns all categories (already filtered: IsActive == true).</summary>
    Task<List<CategoryViewModel>> GetAllCategoriesAsync();

    /// <summary>Alias for GetAllCategoriesAsync — returns only active categories.</summary>
    Task<List<CategoryViewModel>> GetActiveCategoriesAsync();

    Task<List<CategoryViewModel>> GetTopLevelCategoriesAsync();

    /// <summary>
    /// Returns all main categories with their SubCategories lists populated.
    /// Default subcategory is always index 0 in each list.
    /// </summary>
    Task<List<CategoryViewModel>> GetCategoriesWithSubcategoriesAsync();

    Task<CategoryViewModel?> GetCategoryByIdAsync(string categoryId);
    Task<CategoryViewModel?> GetCategoryBySlugAsync(string slug);

    Task<string> CreateCategoryAsync(CreateCategoryRequest request);
    Task<bool> UpdateCategoryAsync(string categoryId, UpdateCategoryRequest request);
    Task<bool> DeleteCategoryAsync(string categoryId);
    Task<int> GetProductCountForCategoryAsync(string categoryId);

    // ==================== SUBCATEGORIES ====================

    Task<List<SubCategoryViewModel>> GetSubCategoriesAsync(string categoryId);
    Task<List<SubCategoryViewModel>> GetSubCategoriesByParentNameAsync(string categoryName);
    Task<SubCategoryViewModel?> GetSubCategoryByIdAsync(string subCategoryId);
    Task<SubCategoryViewModel?> GetDefaultSubCategoryAsync(string categoryId);

    Task<string> CreateSubCategoryAsync(CreateSubCategoryRequest request);
    Task<bool> UpdateSubCategoryAsync(string subCategoryId, UpdateSubCategoryRequest request);
    Task<bool> DeleteSubCategoryAsync(string subCategoryId);

    /// <summary>
    /// Sets the given subcategory as the default for its parent category.
    /// Automatically demotes the previous default.
    /// </summary>
    Task<bool> SetDefaultSubCategoryAsync(string subCategoryId, string categoryId);
}

// ==================== REQUEST DTOs ====================

public class CreateCategoryRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public SvgType IconSvgType { get; set; } = SvgType.None;
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

public class CreateSubCategoryRequest
{
    public string CategoryId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public SvgType IconSvgType { get; set; } = SvgType.None;
    public bool IsDefault { get; set; } = false;
    public int DisplayOrder { get; set; }
}

public class UpdateSubCategoryRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public SvgType? IconSvgType { get; set; }
    public int? DisplayOrder { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsDefault { get; set; }
}
