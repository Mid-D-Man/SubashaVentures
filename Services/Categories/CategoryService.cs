using SubashaVentures.Domain.Product;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Models.Firebase;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.ObjectPooling;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Categories;

public class CategoryService : ICategoryService, IDisposable
{
    private readonly IFirestoreService _firestore;
    private readonly ILogger<CategoryService> _logger;
    private const string COLLECTION = "categories";
    
    // Object pool for category lists
    private MID_ComponentObjectPool<List<CategoryViewModel>>? _categoryListPool;
    private MID_ComponentObjectPool<List<CategoryModel>>? _categoryModelListPool;

    public CategoryService(
        IFirestoreService firestore,
        ILogger<CategoryService> logger)
    {
        _firestore = firestore;
        _logger = logger;
        
        // Initialize object pools
        _categoryListPool = new MID_ComponentObjectPool<List<CategoryViewModel>>(
            () => new List<CategoryViewModel>(50),
            list => list.Clear(),
            maxPoolSize: 5
        );
        
        _categoryModelListPool = new MID_ComponentObjectPool<List<CategoryModel>>(
            () => new List<CategoryModel>(50),
            list => list.Clear(),
            maxPoolSize: 5
        );
    }

    public async Task<List<CategoryViewModel>> GetAllCategoriesAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync("Fetching all categories", LogLevel.Info);
            
            var categories = await _firestore.GetCollectionAsync<CategoryModel>(COLLECTION);
            
            if (categories == null || !categories.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync("No categories found", LogLevel.Warning);
                return new List<CategoryViewModel>();
            }
            
            // Use object pool for result list
            using var pooledList = _categoryListPool?.GetPooled();
            var result = pooledList?.Object ?? new List<CategoryViewModel>();
            
            foreach (var category in categories.Where(c => c.IsActive))
            {
                result.Add(MapToViewModel(category));
            }
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {result.Count} active categories",
                LogLevel.Info
            );
            
            return result.ToList(); // Return copy, not pooled instance
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting all categories");
            _logger.LogError(ex, "Failed to get all categories");
            return new List<CategoryViewModel>();
        }
    }

    public async Task<CategoryViewModel?> GetCategoryByIdAsync(string categoryId)
    {
        try
        {
            if (string.IsNullOrEmpty(categoryId))
            {
                await MID_HelperFunctions.DebugMessageAsync("Category ID is null or empty", LogLevel.Warning);
                return null;
            }
            
            var category = await _firestore.GetDocumentAsync<CategoryModel>(COLLECTION, categoryId);
            
            if (category == null)
            {
                await MID_HelperFunctions.DebugMessageAsync($"Category not found: {categoryId}", LogLevel.Warning);
                return null;
            }
            
            return MapToViewModel(category);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting category: {categoryId}");
            return null;
        }
    }

    public async Task<CategoryViewModel?> GetCategoryBySlugAsync(string slug)
    {
        try
        {
            if (string.IsNullOrEmpty(slug))
            {
                await MID_HelperFunctions.DebugMessageAsync("Slug is null or empty", LogLevel.Warning);
                return null;
            }
            
            var categories = await _firestore.QueryCollectionAsync<CategoryModel>(
                COLLECTION, 
                "slug", 
                slug.ToLowerInvariant()
            );
            
            var category = categories?.FirstOrDefault();
            
            if (category == null)
            {
                await MID_HelperFunctions.DebugMessageAsync($"Category not found for slug: {slug}", LogLevel.Warning);
                return null;
            }
            
            return MapToViewModel(category);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting category by slug: {slug}");
            return null;
        }
    }

    public async Task<List<CategoryViewModel>> GetTopLevelCategoriesAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync("Fetching top-level categories", LogLevel.Info);
            
            var allCategories = await GetAllCategoriesAsync();
            
            var topLevel = allCategories
                .Where(c => string.IsNullOrEmpty(c.ParentId))
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name)
                .ToList();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Found {topLevel.Count} top-level categories",
                LogLevel.Info
            );
            
            return topLevel;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting top-level categories");
            return new List<CategoryViewModel>();
        }
    }

    public async Task<List<CategoryViewModel>> GetSubCategoriesAsync(string parentId)
    {
        try
        {
            if (string.IsNullOrEmpty(parentId))
            {
                await MID_HelperFunctions.DebugMessageAsync("Parent ID is null or empty", LogLevel.Warning);
                return new List<CategoryViewModel>();
            }
            
            await MID_HelperFunctions.DebugMessageAsync($"Fetching subcategories for: {parentId}", LogLevel.Info);
            
            var allCategories = await GetAllCategoriesAsync();
            
            var subCategories = allCategories
                .Where(c => c.ParentId == parentId)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name)
                .ToList();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Found {subCategories.Count} subcategories",
                LogLevel.Info
            );
            
            return subCategories;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting subcategories for: {parentId}");
            return new List<CategoryViewModel>();
        }
    }

    public async Task<string> CreateCategoryAsync(CreateCategoryRequest request)
    {
        try
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("Category name is required", nameof(request));
            
            var categoryModel = new CategoryModel
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name.Trim(),
                Slug = GenerateSlug(request.Name),
                Description = request.Description?.Trim(),
                ImageUrl = request.ImageUrl?.Trim(),
                IconSvgType = request.IconSvgType,
                ParentId = request.ParentId?.Trim(),
                DisplayOrder = request.DisplayOrder,
                IsActive = true,
                ProductCount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            };
            
            var id = await _firestore.AddDocumentAsync(COLLECTION, categoryModel, categoryModel.Id);
            
            if (!string.IsNullOrEmpty(id))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Category created: {request.Name} (ID: {id}, Icon: {request.IconSvgType})",
                    LogLevel.Info
                );
            }
            
            return id ?? string.Empty;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Creating category: {request.Name}");
            _logger.LogError(ex, "Failed to create category: {Name}", request.Name);
            return string.Empty;
        }
    }

    public async Task<bool> UpdateCategoryAsync(string categoryId, UpdateCategoryRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(categoryId))
                throw new ArgumentException("Category ID is required", nameof(categoryId));
            
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            
            var existing = await _firestore.GetDocumentAsync<CategoryModel>(COLLECTION, categoryId);
            
            if (existing == null)
            {
                await MID_HelperFunctions.DebugMessageAsync($"Category not found: {categoryId}", LogLevel.Warning);
                return false;
            }
            
            // Update only provided fields
            var updated = new CategoryModel
            {
                Id = existing.Id,
                Name = request.Name?.Trim() ?? existing.Name,
                Slug = !string.IsNullOrEmpty(request.Name) ? GenerateSlug(request.Name) : existing.Slug,
                Description = request.Description ?? existing.Description,
                ImageUrl = request.ImageUrl ?? existing.ImageUrl,
                IconSvgType = request.IconSvgType ?? existing.IconSvgType,
                ParentId = existing.ParentId,
                DisplayOrder = request.DisplayOrder ?? existing.DisplayOrder,
                IsActive = request.IsActive ?? existing.IsActive,
                ProductCount = existing.ProductCount,
                CreatedAt = existing.CreatedAt,
                UpdatedAt = DateTime.UtcNow
            };
            
            var success = await _firestore.UpdateDocumentAsync(COLLECTION, categoryId, updated);
            
            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Category updated: {updated.Name} (Icon: {updated.IconSvgType})",
                    LogLevel.Info
                );
            }
            
            return success;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Updating category: {categoryId}");
            _logger.LogError(ex, "Failed to update category: {Id}", categoryId);
            return false;
        }
    }

    public async Task<bool> DeleteCategoryAsync(string categoryId)
    {
        try
        {
            if (string.IsNullOrEmpty(categoryId))
                throw new ArgumentException("Category ID is required", nameof(categoryId));
            
            // Check if category has products
            var productCount = await GetProductCountForCategoryAsync(categoryId);
            
            if (productCount > 0)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Cannot delete category with {productCount} products",
                    LogLevel.Warning
                );
                return false;
            }
            
            var success = await _firestore.DeleteDocumentAsync(COLLECTION, categoryId);
            
            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Category deleted: {categoryId}",
                    LogLevel.Info
                );
            }
            
            return success;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Deleting category: {categoryId}");
            _logger.LogError(ex, "Failed to delete category: {Id}", categoryId);
            return false;
        }
    }

    public async Task<int> GetProductCountForCategoryAsync(string categoryId)
    {
        try
        {
            if (string.IsNullOrEmpty(categoryId))
                return 0;
            
            var category = await _firestore.GetDocumentAsync<CategoryModel>(COLLECTION, categoryId);
            return category?.ProductCount ?? 0;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting product count for category: {categoryId}");
            return 0;
        }
    }

    // ==================== PRIVATE HELPER METHODS ====================

    private CategoryViewModel MapToViewModel(CategoryModel model)
    {
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
            SubCategories = new List<CategoryViewModel>() // Populated separately if needed
        };
    }

    private string GenerateSlug(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;
        
        return name
            .ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("'", "")
            .Replace("\"", "")
            .Replace("&", "and")
            .Replace("/", "-")
            .Replace("\\", "-")
            .Trim('-');
    }

    public void Dispose()
    {
        _categoryListPool?.Dispose();
        _categoryModelListPool?.Dispose();
    }
}
