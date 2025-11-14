using SubashaVentures.Domain.Product;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Services.SupaBase;
using System.Text;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using SupabaseProductModel = SubashaVentures.Models.Supabase.ProductModel;
using FirebaseCategoryModel = SubashaVentures.Models.Firebase.CategoryModel;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Products;

public class ProductService : IProductService
{
    private readonly ISupabaseDatabaseService _database;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        ISupabaseDatabaseService database,
        ILogger<ProductService> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task<ProductViewModel?> CreateProductAsync(CreateProductRequest request)
    {
        try
        {
            // 🔴 CRITICAL FIX: Generate ID first, then use it throughout
            var productId = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;

            await MID_HelperFunctions.DebugMessageAsync(
                $"Creating product: ID={productId}, Name={request.Name}",
                LogLevel.Info
            );

            // 🔴 FIX: Create ProductModel with the ID
            var productModel = new SupabaseProductModel
            {
                Id = productId,  // ✓ CRITICAL: Set ID here
                Name = request.Name,
                Slug = GenerateSlug(request.Name),
                Description = request.Description ?? "",
                LongDescription = request.LongDescription ?? "",
                Price = request.Price,
                OriginalPrice = request.OriginalPrice,
                IsOnSale = request.OriginalPrice.HasValue && request.OriginalPrice > request.Price,
                Discount = CalculateDiscount(request.Price, request.OriginalPrice),
                Images = request.ImageUrls ?? new List<string>(),
                VideoUrl = request.VideoUrl,
                Sizes = request.Sizes ?? new List<string>(),
                Colors = request.Colors ?? new List<string>(),
                Stock = request.Stock,
                Sku = request.Sku,
                CategoryId = request.CategoryId,
                Category = request.Category ?? "",
                SubCategory = request.SubCategory,
                Brand = request.Brand ?? "",
                Tags = request.Tags ?? new List<string>(),
                Rating = 0,
                ReviewCount = 0,
                ViewCount = 0,
                ClickCount = 0,
                AddToCartCount = 0,
                PurchaseCount = 0,
                SalesCount = 0,
                TotalRevenue = 0,
                IsActive = true,
                IsFeatured = request.IsFeatured,
                CreatedAt = now,
                CreatedBy = "system",
                IsDeleted = false
            };

            _logger.LogInformation("Inserting product with ID: {ProductId}", productId);

            // Insert into database
            var result = await _database.InsertAsync(productModel);

            if (result == null || !result.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Insert returned null/empty result",
                    LogLevel.Error
                );
                return null;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Product created successfully: {request.Name}",
                LogLevel.Info
            );

            return MapToViewModel(result.First());
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Creating product: {request.Name}");
            _logger.LogError(ex, "Failed to create product: {Name}", request.Name);
            return null;
        }
    }

    public async Task<bool> UpdateProductAsync(string productId, UpdateProductRequest request)
    {
        try
        {
            var existingProduct = await _database.GetByIdAsync<ProductModel>(
                int.TryParse(productId, out var id) ? id : 0
            );

            if (existingProduct == null)
            {
                _logger.LogWarning("Product not found: {ProductId}", productId);
                return false;
            }

            // Update only provided fields
            if (request.Name != null) existingProduct.Name = request.Name;
            if (request.Description != null) existingProduct.Description = request.Description;
            if (request.LongDescription != null) existingProduct.LongDescription = request.LongDescription;
            if (request.Price.HasValue) existingProduct.Price = request.Price.Value;
            if (request.OriginalPrice.HasValue) existingProduct.OriginalPrice = request.OriginalPrice;
            if (request.Stock.HasValue) existingProduct.Stock = request.Stock.Value;
            if (request.CategoryId != null) existingProduct.CategoryId = request.CategoryId;
            if (request.Brand != null) existingProduct.Brand = request.Brand;
            if (request.Tags != null) existingProduct.Tags = request.Tags;
            if (request.Sizes != null) existingProduct.Sizes = request.Sizes;
            if (request.Colors != null) existingProduct.Colors = request.Colors;
            if (request.IsFeatured.HasValue) existingProduct.IsFeatured = request.IsFeatured.Value;
            if (request.IsActive.HasValue) existingProduct.IsActive = request.IsActive.Value;

            existingProduct.UpdatedAt = DateTime.UtcNow;
            existingProduct.UpdatedBy = "system";

            var result = await _database.UpdateAsync(existingProduct);
            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Updating product: {productId}");
            return false;
        }
    }

    public async Task<bool> DeleteProductAsync(string productId)
    {
        try
        {
            var product = await _database.GetByIdAsync<ProductModel>(
                int.TryParse(productId, out var id) ? id : 0
            );

            if (product == null) return false;

            // Soft delete
            product.IsDeleted = true;
            product.DeletedAt = DateTime.UtcNow;
            product.DeletedBy = "system";

            var result = await _database.UpdateAsync(product);
            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Deleting product: {productId}");
            return false;
        }
    }

    public async Task<bool> DeleteProductsAsync(List<string> productIds)
    {
        try
        {
            foreach (var id in productIds)
            {
                await DeleteProductAsync(id);
            }
            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Bulk delete products");
            return false;
        }
    }

    public async Task<ProductViewModel?> GetProductByIdAsync(string productId)
    {
        try
        {
            var product = await _database.GetByIdAsync<ProductModel>(
                int.TryParse(productId, out var id) ? id : 0
            );

            return product != null ? MapToViewModel(product) : null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting product: {productId}");
            return null;
        }
    }

    public async Task<List<ProductViewModel>> GetProductsAsync(int skip = 0, int take = 100)
    {
        try
        {
            var products = await _database.GetAllAsync<ProductModel>();
            
            return products
                .Where(p => !p.IsDeleted)
                .Skip(skip)
                .Take(take)
                .Select(MapToViewModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting products");
            return new List<ProductViewModel>();
        }
    }

    public async Task<bool> UpdateProductStockAsync(string productId, int newStock)
    {
        try
        {
            var product = await _database.GetByIdAsync<ProductModel>(
                int.TryParse(productId, out var id) ? id : 0
            );

            if (product == null) return false;

            product.Stock = newStock;
            product.UpdatedAt = DateTime.UtcNow;

            var result = await _database.UpdateAsync(product);
            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Updating stock: {productId}");
            return false;
        }
    }

    public string GenerateUniqueSku()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = Guid.NewGuid().ToString("N").Substring(0, 4).ToUpper();
        return $"PROD-{timestamp}-{random}";
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
            IsOnSale = model.IsOnSale,
            Discount = model.Discount,
            Images = model.Images,
            VideoUrl = model.VideoUrl,
            Sizes = model.Sizes,
            Colors = model.Colors,
            Stock = model.Stock,
            Sku = model.Sku,
            CategoryId = model.CategoryId,
            Category = model.Category,
            SubCategory = model.SubCategory,
            Brand = model.Brand,
            Tags = model.Tags,
            Rating = model.Rating,
            ReviewCount = model.ReviewCount,
            ViewCount = model.ViewCount,
            SalesCount = model.SalesCount,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
            IsActive = model.IsActive,
            IsFeatured = model.IsFeatured
        };
    }

    private string GenerateSlug(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("&", "and")
            .Replace("'", "")
            .Replace("\"", "");
    }

    private int CalculateDiscount(decimal price, decimal? originalPrice)
    {
        if (!originalPrice.HasValue || originalPrice <= price) return 0;
        return (int)Math.Round(((originalPrice.Value - price) / originalPrice.Value) * 100);
    }
}
