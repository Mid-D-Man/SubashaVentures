// Services/Categories/CategoryService.cs
namespace SubashaVentures.Services.Categories;

using SubashaVentures.Domain.Product;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Models.Firebase;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

public class CategoryService : ICategoryService, IDisposable
{
    private readonly IFirestoreService _firestore;
    private readonly ILogger<CategoryService> _logger;

    private const string CAT_COLLECTION = "categories";
    private const string SUB_COLLECTION = "subcategories";

    public CategoryService(IFirestoreService firestore, ILogger<CategoryService> logger)
    {
        _firestore = firestore;
        _logger    = logger;
    }

    // ==================== CATEGORIES ====================

    public async Task<List<CategoryViewModel>> GetAllCategoriesAsync()
    {
        try
        {
            var models = await _firestore.GetCollectionAsync<CategoryModel>(CAT_COLLECTION);
            if (models == null || !models.Any()) return new List<CategoryViewModel>();

            return models
                .Where(c => c.IsActive)
                .Select(CategoryViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetAllCategoriesAsync");
            return new List<CategoryViewModel>();
        }
    }

    /// <summary>
    /// Alias for GetAllCategoriesAsync — returns only active categories.
    /// Added so callers can be explicit about intent without breaking existing code.
    /// </summary>
    public async Task<List<CategoryViewModel>> GetActiveCategoriesAsync()
        => await GetAllCategoriesAsync();

    public async Task<List<CategoryViewModel>> GetTopLevelCategoriesAsync()
    {
        try
        {
            var all = await GetAllCategoriesAsync();
            return all
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetTopLevelCategoriesAsync");
            return new List<CategoryViewModel>();
        }
    }

    public async Task<List<CategoryViewModel>> GetCategoriesWithSubcategoriesAsync()
    {
        try
        {
            var categories = await GetAllCategoriesAsync();
            var allSubs    = await GetAllSubCategoriesRawAsync();

            foreach (var cat in categories)
            {
                cat.SubCategories = allSubs
                    .Where(s => s.CategoryId == cat.Id && s.IsActive)
                    .OrderByDescending(s => s.IsDefault)
                    .ThenBy(s => s.DisplayOrder)
                    .ThenBy(s => s.Name)
                    .ToList();
            }

            return categories
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetCategoriesWithSubcategoriesAsync");
            return new List<CategoryViewModel>();
        }
    }

    public async Task<CategoryViewModel?> GetCategoryByIdAsync(string categoryId)
    {
        try
        {
            if (string.IsNullOrEmpty(categoryId)) return null;
            var model = await _firestore.GetDocumentAsync<CategoryModel>(CAT_COLLECTION, categoryId);
            return model == null ? null : CategoryViewModel.FromCloudModel(model);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"GetCategoryByIdAsync: {categoryId}");
            return null;
        }
    }

    public async Task<CategoryViewModel?> GetCategoryBySlugAsync(string slug)
    {
        try
        {
            if (string.IsNullOrEmpty(slug)) return null;
            var results = await _firestore.QueryCollectionAsync<CategoryModel>(
                CAT_COLLECTION, "slug", slug.ToLowerInvariant());
            var model = results?.FirstOrDefault();
            return model == null ? null : CategoryViewModel.FromCloudModel(model);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"GetCategoryBySlugAsync: {slug}");
            return null;
        }
    }

    public async Task<string> CreateCategoryAsync(CreateCategoryRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("Category name is required");

            var model = new CategoryModel
            {
                Id           = Guid.NewGuid().ToString(),
                Name         = request.Name.Trim(),
                Slug         = GenerateSlug(request.Name),
                Description  = request.Description?.Trim(),
                ImageUrl     = request.ImageUrl?.Trim(),
                IconSvgType  = request.IconSvgType,
                DisplayOrder = request.DisplayOrder,
                IsActive     = true,
                ProductCount = 0,
                CreatedAt    = DateTime.UtcNow
            };

            var id = await _firestore.AddDocumentAsync(CAT_COLLECTION, model, model.Id);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Category created: {model.Name} ({id})", LogLevel.Info);

            return id ?? string.Empty;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"CreateCategoryAsync: {request.Name}");
            return string.Empty;
        }
    }

    public async Task<bool> UpdateCategoryAsync(string categoryId, UpdateCategoryRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(categoryId)) return false;

            var existing = await _firestore.GetDocumentAsync<CategoryModel>(CAT_COLLECTION, categoryId);
            if (existing == null) return false;

            var updated = new CategoryModel
            {
                Id           = existing.Id,
                Name         = request.Name?.Trim()                                    ?? existing.Name,
                Slug         = request.Name != null ? GenerateSlug(request.Name)       : existing.Slug,
                Description  = request.Description                                     ?? existing.Description,
                ImageUrl     = request.ImageUrl                                         ?? existing.ImageUrl,
                IconSvgType  = request.IconSvgType                                     ?? existing.IconSvgType,
                DisplayOrder = request.DisplayOrder                                    ?? existing.DisplayOrder,
                IsActive     = request.IsActive                                        ?? existing.IsActive,
                ProductCount = existing.ProductCount,
                CreatedAt    = existing.CreatedAt,
                UpdatedAt    = DateTime.UtcNow
            };

            return await _firestore.UpdateDocumentAsync(CAT_COLLECTION, categoryId, updated);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"UpdateCategoryAsync: {categoryId}");
            return false;
        }
    }

    public async Task<bool> DeleteCategoryAsync(string categoryId)
    {
        try
        {
            if (string.IsNullOrEmpty(categoryId)) return false;

            var category = await GetCategoryByIdAsync(categoryId);
            if (category == null) return false;

            if (category.ProductCount > 0)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Cannot delete category with products: {category.Name}", LogLevel.Warning);
                return false;
            }

            var subs = await GetSubCategoriesAsync(categoryId);
            if (subs.Any(s => s.ProductCount > 0))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Cannot delete — a subcategory under {category.Name} has products",
                    LogLevel.Warning);
                return false;
            }

            foreach (var sub in subs)
                await _firestore.DeleteDocumentAsync(SUB_COLLECTION, sub.Id);

            return await _firestore.DeleteDocumentAsync(CAT_COLLECTION, categoryId);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"DeleteCategoryAsync: {categoryId}");
            return false;
        }
    }

    public async Task<int> GetProductCountForCategoryAsync(string categoryId)
    {
        try
        {
            if (string.IsNullOrEmpty(categoryId)) return 0;
            var model = await _firestore.GetDocumentAsync<CategoryModel>(CAT_COLLECTION, categoryId);
            return model?.ProductCount ?? 0;
        }
        catch { return 0; }
    }

    // ==================== SUBCATEGORIES ====================

    public async Task<List<SubCategoryViewModel>> GetSubCategoriesAsync(string categoryId)
    {
        try
        {
            if (string.IsNullOrEmpty(categoryId)) return new List<SubCategoryViewModel>();

            var results = await _firestore.QueryCollectionAsync<SubCategoryModel>(
                SUB_COLLECTION, "categoryId", categoryId);

            return (results ?? new List<SubCategoryModel>())
                .Where(s => s.IsActive)
                .Select(SubCategoryViewModel.FromCloudModel)
                .OrderByDescending(s => s.IsDefault)
                .ThenBy(s => s.DisplayOrder)
                .ThenBy(s => s.Name)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"GetSubCategoriesAsync: {categoryId}");
            return new List<SubCategoryViewModel>();
        }
    }

    public async Task<List<SubCategoryViewModel>> GetSubCategoriesByParentNameAsync(string categoryName)
    {
        try
        {
            if (string.IsNullOrEmpty(categoryName)) return new List<SubCategoryViewModel>();

            var parent = (await GetAllCategoriesAsync())
                .FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));

            if (parent == null) return new List<SubCategoryViewModel>();

            return await GetSubCategoriesAsync(parent.Id);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(
                ex, $"GetSubCategoriesByParentNameAsync: {categoryName}");
            return new List<SubCategoryViewModel>();
        }
    }

    public async Task<SubCategoryViewModel?> GetSubCategoryByIdAsync(string subCategoryId)
    {
        try
        {
            if (string.IsNullOrEmpty(subCategoryId)) return null;
            var model = await _firestore.GetDocumentAsync<SubCategoryModel>(
                SUB_COLLECTION, subCategoryId);
            return model == null ? null : SubCategoryViewModel.FromCloudModel(model);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(
                ex, $"GetSubCategoryByIdAsync: {subCategoryId}");
            return null;
        }
    }

    public async Task<SubCategoryViewModel?> GetDefaultSubCategoryAsync(string categoryId)
    {
        try
        {
            var subs = await GetSubCategoriesAsync(categoryId);
            return subs.FirstOrDefault(s => s.IsDefault) ?? subs.FirstOrDefault();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(
                ex, $"GetDefaultSubCategoryAsync: {categoryId}");
            return null;
        }
    }

    public async Task<string> CreateSubCategoryAsync(CreateSubCategoryRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("Subcategory name is required");

            if (string.IsNullOrEmpty(request.CategoryId))
                throw new ArgumentException("CategoryId is required");

            var parent = await GetCategoryByIdAsync(request.CategoryId);
            if (parent == null)
                throw new ArgumentException($"Parent category not found: {request.CategoryId}");

            if (request.IsDefault)
                await DemoteExistingDefaultAsync(request.CategoryId);

            var model = new SubCategoryModel
            {
                Id           = Guid.NewGuid().ToString(),
                CategoryId   = request.CategoryId,
                Name         = request.Name.Trim(),
                Slug         = GenerateSlug($"{parent.Slug}-{request.Name}"),
                Description  = request.Description?.Trim(),
                ImageUrl     = request.ImageUrl?.Trim(),
                IconSvgType  = request.IconSvgType,
                IsDefault    = request.IsDefault,
                DisplayOrder = request.DisplayOrder,
                IsActive     = true,
                ProductCount = 0,
                CreatedAt    = DateTime.UtcNow
            };

            var id = await _firestore.AddDocumentAsync(SUB_COLLECTION, model, model.Id);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Subcategory created: {model.Name} under {parent.Name} (default={model.IsDefault})",
                LogLevel.Info);

            return id ?? string.Empty;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"CreateSubCategoryAsync: {request.Name}");
            return string.Empty;
        }
    }

    public async Task<bool> UpdateSubCategoryAsync(string subCategoryId, UpdateSubCategoryRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(subCategoryId)) return false;

            var existing = await _firestore.GetDocumentAsync<SubCategoryModel>(
                SUB_COLLECTION, subCategoryId);
            if (existing == null) return false;

            if (request.IsDefault == true)
                await DemoteExistingDefaultAsync(existing.CategoryId);

            var updated = new SubCategoryModel
            {
                Id           = existing.Id,
                CategoryId   = existing.CategoryId,
                Name         = request.Name?.Trim()                               ?? existing.Name,
                Slug         = request.Name != null ? GenerateSlug(request.Name) : existing.Slug,
                Description  = request.Description                                ?? existing.Description,
                ImageUrl     = request.ImageUrl                                   ?? existing.ImageUrl,
                IconSvgType  = request.IconSvgType                                ?? existing.IconSvgType,
                IsDefault    = request.IsDefault                                  ?? existing.IsDefault,
                DisplayOrder = request.DisplayOrder                               ?? existing.DisplayOrder,
                IsActive     = request.IsActive                                   ?? existing.IsActive,
                ProductCount = existing.ProductCount,
                CreatedAt    = existing.CreatedAt,
                UpdatedAt    = DateTime.UtcNow
            };

            return await _firestore.UpdateDocumentAsync(SUB_COLLECTION, subCategoryId, updated);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(
                ex, $"UpdateSubCategoryAsync: {subCategoryId}");
            return false;
        }
    }

    public async Task<bool> DeleteSubCategoryAsync(string subCategoryId)
    {
        try
        {
            if (string.IsNullOrEmpty(subCategoryId)) return false;

            var sub = await GetSubCategoryByIdAsync(subCategoryId);
            if (sub == null) return false;

            if (sub.ProductCount > 0)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Cannot delete subcategory with products: {sub.Name}", LogLevel.Warning);
                return false;
            }

            return await _firestore.DeleteDocumentAsync(SUB_COLLECTION, subCategoryId);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(
                ex, $"DeleteSubCategoryAsync: {subCategoryId}");
            return false;
        }
    }

    public async Task<bool> SetDefaultSubCategoryAsync(string subCategoryId, string categoryId)
    {
        try
        {
            if (string.IsNullOrEmpty(subCategoryId) || string.IsNullOrEmpty(categoryId))
                return false;

            await DemoteExistingDefaultAsync(categoryId);

            var target = await _firestore.GetDocumentAsync<SubCategoryModel>(
                SUB_COLLECTION, subCategoryId);
            if (target == null || target.CategoryId != categoryId) return false;

            target.IsDefault  = true;
            target.UpdatedAt  = DateTime.UtcNow;

            return await _firestore.UpdateDocumentAsync(SUB_COLLECTION, subCategoryId, target);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(
                ex, $"SetDefaultSubCategoryAsync: {subCategoryId}");
            return false;
        }
    }

    // ==================== PRIVATE ====================

    private async Task<List<SubCategoryViewModel>> GetAllSubCategoriesRawAsync()
    {
        try
        {
            var models = await _firestore.GetCollectionAsync<SubCategoryModel>(SUB_COLLECTION);
            if (models == null) return new List<SubCategoryViewModel>();
            return models.Select(SubCategoryViewModel.FromCloudModel).ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetAllSubCategoriesRawAsync");
            return new List<SubCategoryViewModel>();
        }
    }

    private async Task DemoteExistingDefaultAsync(string categoryId)
    {
        try
        {
            var allSubs = await _firestore.GetCollectionAsync<SubCategoryModel>(SUB_COLLECTION);
            var currentDefault = allSubs?.FirstOrDefault(s =>
                s.CategoryId == categoryId && s.IsDefault);

            if (currentDefault == null) return;

            currentDefault.IsDefault  = false;
            currentDefault.UpdatedAt  = DateTime.UtcNow;
            await _firestore.UpdateDocumentAsync(SUB_COLLECTION, currentDefault.Id, currentDefault);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(
                ex, $"DemoteExistingDefaultAsync: {categoryId}");
        }
    }

    private static string GenerateSlug(string name) =>
        name.ToLowerInvariant()
            .Replace(" ",  "-")
            .Replace("'",  "")
            .Replace("\"", "")
            .Replace("&",  "and")
            .Replace("/",  "-")
            .Replace("\\", "-")
            .Trim('-');

    public void Dispose() { }
}
