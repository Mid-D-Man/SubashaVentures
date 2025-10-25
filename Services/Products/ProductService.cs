
// ===== IMPLEMENTATION =====

using SubashaVentures.Domain.Product;
using SubashaVentures.Models.Firebase;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Services.Supabase;

namespace SubashaVentures.Services.Products;

public class ProductService : IProductService
{
    private readonly IFirestoreService _firestoreService;
    private readonly ISupabaseStorageService _storageService;
    private readonly ILogger<ProductService> _logger;
    
    private const string ProductsCollection = "products";
    private const string CategoriesCollection = "categories";

    public ProductService(
        IFirestoreService firestoreService,
        ISupabaseStorageService storageService,
        ILogger<ProductService> logger)
    {
        _firestoreService = firestoreService;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<ProductViewModel?> GetProductAsync(string id)
    {
        try
        {
            var product = await _firestoreService.GetDocumentAsync<ProductModel>(ProductsCollection, id);
            return product != null ? MapToViewModel(product) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product: {Id}", id);
            return null;
        }
    }

    public async Task<List<ProductViewModel>> GetProductsAsync(int skip = 0, int take = 50)
    {
        try
        {
            var products = await _firestoreService.GetCollectionAsync<ProductModel>(ProductsCollection);
            
            return products
                .Skip(skip)
                .Take(take)
                .Select(MapToViewModel)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting products");
            return new List<ProductViewModel>();
        }
    }

    public async Task<List<ProductViewModel>> SearchProductsAsync(string query)
    {
        try
        {
            var products = await _firestoreService.GetCollectionAsync<ProductModel>(ProductsCollection);
            
            var searchLower = query.ToLower();
            return products
                .Where(p => p.Name.ToLower().Contains(searchLower) ||
                           p.Description.ToLower().Contains(searchLower))
                .Select(MapToViewModel)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching products");
            return new List<ProductViewModel>();
        }
    }

    public async Task<List<ProductViewModel>> GetProductsByCategoryAsync(string categoryId)
    {
        try
        {
            var products = await _firestoreService.QueryCollectionAsync<ProductModel>(
                ProductsCollection,
                "categoryId",
                categoryId);
            
            return products.Select(MapToViewModel).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting products by category");
            return new List<ProductViewModel>();
        }
    }

    public async Task<int> GetProductCountAsync()
    {
        try
        {
            var products = await _firestoreService.GetCollectionAsync<ProductModel>(ProductsCollection);
            return products.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product count");
            return 0;
        }
    }

    public async Task<ProductViewModel?> CreateProductAsync(CreateProductRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Name))
            {
                throw new ArgumentException("Product name is required");
            }

            var productModel = new ProductModel
            {
                Name = request.Name,
                Slug = GenerateSlug(request.Name),
                Description = request.Description,
                LongDescription = request.LongDescription,
                Price = request.Price,
                OriginalPrice = request.OriginalPrice,
                Stock = request.Stock,
                Sku = request.Sku,
                CategoryId = request.CategoryId,
                Category = await GetCategoryNameAsync(request.CategoryId),
                Brand = request.Brand,
                Tags = request.Tags ?? new List<string>(),
                Sizes = request.Sizes ?? new List<string>(),
                Colors = request.Colors ?? new List<string>(),
                Images = request.ImageUrls ?? new List<string>(),
                IsFeatured = request.IsFeatured,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                ViewCount = 0,
                SalesCount = 0,
                Rating = 0,
                ReviewCount = 0,
                IsOnSale = request.OriginalPrice.HasValue && request.OriginalPrice > request.Price,
                Discount = request.OriginalPrice.HasValue 
                    ? (int)Math.Round(((request.OriginalPrice.Value - request.Price) / request.OriginalPrice.Value) * 100)
                    : 0
            };

            var productId = await _firestoreService.AddDocumentAsync(ProductsCollection, productModel);
            
            _logger.LogInformation("Product created: {Name} (ID: {Id})", request.Name, productId);
            
            return await GetProductAsync(productId ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product");
            return null;
        }
    }

    public async Task<bool> UpdateProductAsync(string id, UpdateProductRequest request)
    {
        try
        {
            var product = await _firestoreService.GetDocumentAsync<ProductModel>(ProductsCollection, id);
            if (product == null)
            {
                return false;
            }

            var updatedProduct = product with
            {
                Name = request.Name ?? product.Name,
                Slug = !string.IsNullOrEmpty(request.Name) ? GenerateSlug(request.Name) : product.Slug,
                Description = request.Description ?? product.Description,
                LongDescription = request.LongDescription ?? product.LongDescription,
                Price = request.Price ?? product.Price,
                OriginalPrice = request.OriginalPrice ?? product.OriginalPrice,
                Stock = request.Stock ?? product.Stock,
                CategoryId = request.CategoryId ?? product.CategoryId,
                Brand = request.Brand ?? product.Brand,
                Tags = request.Tags ?? product.Tags,
                Sizes = request.Sizes ?? product.Sizes,
                Colors = request.Colors ?? product.Colors,
                IsFeatured = request.IsFeatured ?? product.IsFeatured,
                IsActive = request.IsActive ?? product.IsActive,
                UpdatedAt = DateTime.UtcNow,
                IsOnSale = (request.OriginalPrice ?? product.OriginalPrice).HasValue && 
                          (request.OriginalPrice ?? product.OriginalPrice) > (request.Price ?? product.Price),
                Discount = (request.OriginalPrice ?? product.OriginalPrice).HasValue
                    ? (int)Math.Round((((request.OriginalPrice ?? product.OriginalPrice).Value - (request.Price ?? product.Price)) / 
                       (request.OriginalPrice ?? product.OriginalPrice).Value) * 100)
                    : product.Discount
            };

            var result = await _firestoreService.UpdateDocumentAsync(ProductsCollection, id, updatedProduct);
            
            _logger.LogInformation("Product updated: {Id}", id);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product: {Id}", id);
            return false;
        }
    }

    public async Task<bool> UpdateProductImagesAsync(string id, List<string> imageUrls)
    {
        try
        {
            return await _firestoreService.UpdateFieldsAsync(
                ProductsCollection,
                id,
                new { Images = imageUrls, UpdatedAt = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product images: {Id}", id);
            return false;
        }
    }

    public async Task<bool> UpdateProductStockAsync(string id, int quantity)
    {
        try
        {
            return await _firestoreService.UpdateFieldsAsync(
                ProductsCollection,
                id,
                new { Stock = quantity, UpdatedAt = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product stock: {Id}", id);
            return false;
        }
    }

    public async Task<bool> DeleteProductAsync(string id)
    {
        try
        {
            // Get product to retrieve image paths
            var product = await _firestoreService.GetDocumentAsync<ProductModel>(ProductsCollection, id);
            
            if (product?.Images.Any() == true)
            {
                await _storageService.DeleteImagesAsync(product.Images);
            }

            var result = await _firestoreService.DeleteDocumentAsync(ProductsCollection, id);
            
            _logger.LogInformation("Product deleted: {Id}", id);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product: {Id}", id);
            return false;
        }
    }

    public async Task<bool> DeleteProductsAsync(List<string> ids)
    {
        try
        {
            foreach (var id in ids)
            {
                await DeleteProductAsync(id);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting multiple products");
            return false;
        }
    }

    public async Task<ProductImageUploadResult> UploadProductImageAsync(Stream imageStream, string fileName)
    {
        try
        {
            var result = await _storageService.UploadImageAsync(imageStream, fileName, "products");
            
            return new ProductImageUploadResult
            {
                Success = result.Success,
                Url = result.PublicUrl,
                FilePath = result.FilePath,
                FileSize = result.FileSize,
                ErrorMessage = result.ErrorMessage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading product image");
            return new ProductImageUploadResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<List<ProductImageUploadResult>> UploadProductImagesAsync(
        List<(Stream stream, string fileName)> files)
    {
        var results = new List<ProductImageUploadResult>();

        foreach (var (stream, fileName) in files)
        {
            var result = await UploadProductImageAsync(stream, fileName);
            results.Add(result);
        }

        return results;
    }

    public async Task<bool> DeleteProductImageAsync(string imagePath)
    {
        try
        {
            return await _storageService.DeleteImageAsync(imagePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product image: {Path}", imagePath);
            return false;
        }
    }

    private string GenerateSlug(string name)
    {
        return name
            .ToLower()
            .Replace(" ", "-")
            .Replace("--", "-")
            .Trim('-');
    }

    private async Task<string> GetCategoryNameAsync(string categoryId)
    {
        try
        {
            if (string.IsNullOrEmpty(categoryId))
                return string.Empty;

            var category = await _firestoreService.GetDocumentAsync<CategoryModel>(
                CategoriesCollection,
                categoryId);
            
            return category?.Name ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private ProductViewModel MapToViewModel(ProductModel model)
    {
        return new ProductViewModel
        {
            Id = model.Id,
            Name = model.Name,
            Slug = model.Slug,
            Description = model.Description,
            LongDescription = model.LongDescription,
            Price = model.Price,
            OriginalPrice = model.OriginalPrice,
            Stock = model.Stock,
            Sku = model.Sku,
            CategoryId = model.CategoryId,
            Category = model.Category,
            Brand = model.Brand,
            Tags = model.Tags,
            Sizes = model.Sizes,
            Colors = model.Colors,
            Images = model.Images,
            Rating = model.Rating,
            ReviewCount = model.ReviewCount,
            IsActive = model.IsActive,
            IsFeatured = model.IsFeatured,
            IsOnSale = model.IsOnSale,
            Discount = model.Discount,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
    }
}