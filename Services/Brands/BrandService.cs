// Services/Brands/BrandService.cs
using SubashaVentures.Models.Firebase;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.ObjectPooling;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Brands;

public class BrandService : IBrandService, IDisposable
{
    private readonly IFirestoreService _firestore;
    private readonly ILogger<BrandService> _logger;
    private const string COLLECTION = "brands";
    
    private MID_ComponentObjectPool<List<BrandModel>>? _brandListPool;

    public BrandService(
        IFirestoreService firestore,
        ILogger<BrandService> logger)
    {
        _firestore = firestore;
        _logger = logger;
        
        _brandListPool = new MID_ComponentObjectPool<List<BrandModel>>(
            () => new List<BrandModel>(50),
            list => list.Clear(),
            maxPoolSize: 5
        );
    }

    public async Task<List<BrandModel>> GetAllBrandsAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync("Fetching all brands", LogLevel.Info);
            
            var brands = await _firestore.GetCollectionAsync<BrandModel>(COLLECTION);
            
            if (brands == null || !brands.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync("No brands found", LogLevel.Warning);
                return new List<BrandModel>();
            }
            
            using var pooledList = _brandListPool?.GetPooled();
            var result = pooledList?.Object ?? new List<BrandModel>();
            
            foreach (var brand in brands.Where(b => b.IsActive))
            {
                result.Add(brand);
            }
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {result.Count} active brands",
                LogLevel.Info
            );
            
            return result.ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting all brands");
            _logger.LogError(ex, "Failed to get all brands");
            return new List<BrandModel>();
        }
    }

    public async Task<BrandModel?> GetBrandByIdAsync(string brandId)
    {
        try
        {
            if (string.IsNullOrEmpty(brandId))
            {
                await MID_HelperFunctions.DebugMessageAsync("Brand ID is null or empty", LogLevel.Warning);
                return null;
            }
            
            var brand = await _firestore.GetDocumentAsync<BrandModel>(COLLECTION, brandId);
            
            if (brand == null)
            {
                await MID_HelperFunctions.DebugMessageAsync($"Brand not found: {brandId}", LogLevel.Warning);
                return null;
            }
            
            return brand;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting brand: {brandId}");
            return null;
        }
    }

    public async Task<BrandModel?> GetBrandByNameAsync(string name)
    {
        try
        {
            if (string.IsNullOrEmpty(name))
            {
                await MID_HelperFunctions.DebugMessageAsync("Brand name is null or empty", LogLevel.Warning);
                return null;
            }
            
            var brands = await _firestore.QueryCollectionAsync<BrandModel>(
                COLLECTION, 
                "name", 
                name
            );
            
            var brand = brands?.FirstOrDefault();
            
            if (brand == null)
            {
                await MID_HelperFunctions.DebugMessageAsync($"Brand not found: {name}", LogLevel.Warning);
                return null;
            }
            
            return brand;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting brand by name: {name}");
            return null;
        }
    }

    public async Task<string> CreateBrandAsync(CreateBrandRequest request)
    {
        try
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("Brand name is required", nameof(request));
            
            // Check if brand already exists
            var existing = await GetBrandByNameAsync(request.Name);
            if (existing != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Brand already exists: {request.Name}",
                    LogLevel.Warning
                );
                return string.Empty;
            }
            
            var brandModel = new BrandModel
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name.Trim(),
                Slug = GenerateSlug(request.Name),
                Description = request.Description?.Trim(),
                LogoUrl = request.LogoUrl?.Trim(),
                WebsiteUrl = request.WebsiteUrl?.Trim(),
                DisplayOrder = request.DisplayOrder,
                IsFeatured = request.IsFeatured,
                IsActive = true,
                ProductCount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            };
            
            var id = await _firestore.AddDocumentAsync(COLLECTION, brandModel, brandModel.Id);
            
            if (!string.IsNullOrEmpty(id))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Brand created: {request.Name} (ID: {id})",
                    LogLevel.Info
                );
            }
            
            return id ?? string.Empty;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Creating brand: {request.Name}");
            _logger.LogError(ex, "Failed to create brand: {Name}", request.Name);
            return string.Empty;
        }
    }

    public async Task<bool> UpdateBrandAsync(string brandId, UpdateBrandRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(brandId))
                throw new ArgumentException("Brand ID is required", nameof(brandId));
            
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            
            var existing = await _firestore.GetDocumentAsync<BrandModel>(COLLECTION, brandId);
            
            if (existing == null)
            {
                await MID_HelperFunctions.DebugMessageAsync($"Brand not found: {brandId}", LogLevel.Warning);
                return false;
            }
            
            var updated = new BrandModel
            {
                Id = existing.Id,
                Name = request.Name?.Trim() ?? existing.Name,
                Slug = !string.IsNullOrEmpty(request.Name) ? GenerateSlug(request.Name) : existing.Slug,
                Description = request.Description ?? existing.Description,
                LogoUrl = request.LogoUrl ?? existing.LogoUrl,
                WebsiteUrl = request.WebsiteUrl ?? existing.WebsiteUrl,
                DisplayOrder = request.DisplayOrder ?? existing.DisplayOrder,
                IsFeatured = request.IsFeatured ?? existing.IsFeatured,
                IsActive = request.IsActive ?? existing.IsActive,
                ProductCount = existing.ProductCount,
                CreatedAt = existing.CreatedAt,
                UpdatedAt = DateTime.UtcNow
            };
            
            var success = await _firestore.UpdateDocumentAsync(COLLECTION, brandId, updated);
            
            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Brand updated: {updated.Name}",
                    LogLevel.Info
                );
            }
            
            return success;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Updating brand: {brandId}");
            _logger.LogError(ex, "Failed to update brand: {Id}", brandId);
            return false;
        }
    }

    public async Task<bool> DeleteBrandAsync(string brandId)
    {
        try
        {
            if (string.IsNullOrEmpty(brandId))
                throw new ArgumentException("Brand ID is required", nameof(brandId));
            
            var productCount = await GetProductCountForBrandAsync(brandId);
            
            if (productCount > 0)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Cannot delete brand with {productCount} products",
                    LogLevel.Warning
                );
                return false;
            }
            
            var success = await _firestore.DeleteDocumentAsync(COLLECTION, brandId);
            
            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Brand deleted: {brandId}",
                    LogLevel.Info
                );
            }
            
            return success;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Deleting brand: {brandId}");
            _logger.LogError(ex, "Failed to delete brand: {Id}", brandId);
            return false;
        }
    }

    public async Task<int> GetProductCountForBrandAsync(string brandId)
    {
        try
        {
            if (string.IsNullOrEmpty(brandId))
                return 0;
            
            var brand = await _firestore.GetDocumentAsync<BrandModel>(COLLECTION, brandId);
            return brand?.ProductCount ?? 0;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting product count for brand: {brandId}");
            return 0;
        }
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
        _brandListPool?.Dispose();
    }
}
