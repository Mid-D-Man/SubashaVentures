using SubashaVentures.Domain.Product;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Services.SupaBase;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Postgrest;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Products;

public class ProductService : IProductService
{
    private readonly ISupabaseDatabaseService _database;
    private readonly ILogger<ProductService> _logger;

    private static readonly List<string> CommonTags = new()
    {
        "New Arrival", "Best Seller", "Trending", "Limited Edition", 
        "Eco-Friendly", "Handmade", "Premium", "Luxury", "Budget-Friendly",
        "Summer Collection", "Winter Collection", "Casual", "Formal", 
        "Sporty", "Vintage", "Modern", "Classic"
    };

    private static readonly List<string> CommonSizes = new()
    {
        "XS", "S", "M", "L", "XL", "XXL", "XXXL",
        "One Size", "Free Size",
        "6", "8", "10", "12", "14", "16", "18",
        "36", "38", "40", "42", "44", "46", "48"
    };

    private static readonly List<string> CommonColors = new()
    {
        "Black", "White", "Gray", "Navy", "Beige", "Brown",
        "Red", "Blue", "Green", "Yellow", "Orange", "Pink", "Purple",
        "Maroon", "Olive", "Teal", "Burgundy", "Khaki", "Cream",
        "Multi-Color", "Print", "Pattern"
    };

    public ProductService(
        ISupabaseDatabaseService database,
        ILogger<ProductService> logger)
    {
        _database = database;
        _logger = logger;
    }

    public List<string> GetCommonTags() => CommonTags;
    public List<string> GetCommonSizes() => CommonSizes;
    public List<string> GetCommonColors() => CommonColors;

    public async Task<List<ProductViewModel>> GetAllProductsAsync()
    {
        try
        {
            var products = await _database.GetAllAsync<ProductModel>();
            return products
                .Where(p => !p.IsDeleted && p.IsActive)
                .Select(MapToViewModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting all products");
            return new List<ProductViewModel>();
        }
    }

    public async Task<List<ProductViewModel>> GetProductsByCategoryAsync(string categoryId)
    {
        try
        {
            var products = await _database.GetWithFilterAsync<ProductModel>(
                "category_id",
                Constants.Operator.Equals,
                categoryId
            );
            
            return products
                .Where(p => !p.IsDeleted && p.IsActive)
                .Select(MapToViewModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting products by category: {categoryId}");
            return new List<ProductViewModel>();
        }
    }

    public async Task<ProductViewModel?> CreateProductAsync(CreateProductRequest request)
    {
        try
        {
            var now = DateTime.UtcNow;

            await MID_HelperFunctions.DebugMessageAsync(
                $"Creating product: Name={request.Name}, SKU={request.Sku}",
                LogLevel.Info
            );

            var existingSku = await GetProductBySkuAsync(request.Sku);
            if (existingSku != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"SKU already exists: {request.Sku}",
                    LogLevel.Error
                );
                return null;
            }

            var productModel = new ProductModel
            {
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

            _logger.LogInformation("Inserting product: {Name}", request.Name);

            var result = await _database.InsertAsync(productModel);

            if (result == null || !result.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Insert returned null/empty result",
                    LogLevel.Error
                );
                return null;
            }

            var createdProduct = result.First();
            var productId = createdProduct.Id;

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Product created with ID: {productId}",
                LogLevel.Info
            );

            var analyticsModel = new ProductAnalyticsModel
            {
                ProductId = productId,
                ProductSku = request.Sku,
                ProductName = request.Name,
                TotalViews = 0,
                TotalClicks = 0,
                TotalAddToCart = 0,
                TotalPurchases = 0,
                TotalRevenue = 0,
                ViewToCartRate = 0,
                CartToPurchaseRate = 0,
                OverallConversionRate = 0,
                IsTrending = false,
                IsBestSeller = false,
                NeedsAttention = false,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _database.InsertAsync(analyticsModel);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Analytics created for product ID: {productId}",
                LogLevel.Info
            );

            return MapToViewModel(createdProduct);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Creating product: {request.Name}");
            _logger.LogError(ex, "Failed to create product: {Name}", request.Name);
            return null;
        }
    }

    public async Task<bool> UpdateProductAsync(int productId, UpdateProductRequest request)
    {
        try
        {
            var existingProduct = await _database.GetByIdAsync<ProductModel>(productId);

            if (existingProduct == null)
            {
                _logger.LogWarning("Product not found: {ProductId}", productId);
                return false;
            }

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
            if (request.ImageUrls != null) existingProduct.Images = request.ImageUrls;
            if (request.VideoUrl != null) existingProduct.VideoUrl = request.VideoUrl;
            if (request.IsFeatured.HasValue) existingProduct.IsFeatured = request.IsFeatured.Value;
            if (request.IsActive.HasValue) existingProduct.IsActive = request.IsActive.Value;

            if (request.Price.HasValue || request.OriginalPrice.HasValue)
            {
                existingProduct.IsOnSale = existingProduct.OriginalPrice.HasValue && 
                                           existingProduct.OriginalPrice > existingProduct.Price;
                existingProduct.Discount = CalculateDiscount(existingProduct.Price, existingProduct.OriginalPrice);
            }
            
            if (request.Name != null)
            {
                existingProduct.Slug = GenerateSlug(request.Name);
            }

            existingProduct.UpdatedAt = DateTime.UtcNow;
            existingProduct.UpdatedBy = "system";

            _logger.LogInformation("Updating product {ProductId}: Images={ImageCount}, Video={HasVideo}",
                productId,
                existingProduct.Images?.Count ?? 0,
                !string.IsNullOrEmpty(existingProduct.VideoUrl));

            var result = await _database.UpdateAsync(existingProduct);
            
            if (request.Name != null)
            {
                var analytics = await GetProductAnalyticsAsync(productId);
                if (analytics != null)
                {
                    analytics.ProductName = request.Name;
                    analytics.UpdatedAt = DateTime.UtcNow;
                    await _database.UpdateAsync(analytics);
                }
            }

            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Updating product: {productId}");
            _logger.LogError(ex, "Failed to update product: {ProductId}", productId);
            return false;
        }
    }

    public async Task<bool> DeleteProductAsync(int productId)
    {
        try
        {
            var product = await _database.GetByIdAsync<ProductModel>(productId);
            if (product == null) return false;

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

    public async Task<bool> DeleteProductsAsync(List<int> productIds)
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

    public async Task<ProductViewModel?> GetProductByIdAsync(int productId)
    {
        try
        {
            var product = await _database.GetByIdAsync<ProductModel>(productId);
            return product != null ? MapToViewModel(product) : null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting product: {productId}");
            return null;
        }
    }

    public async Task<ProductViewModel?> GetProductBySkuAsync(string sku)
    {
        try
        {
            var products = await _database.GetWithFilterAsync<ProductModel>(
                "sku", 
                Constants.Operator.Equals, 
                sku
            );
            
            var product = products.FirstOrDefault();
            return product != null ? MapToViewModel(product) : null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting product by SKU: {sku}");
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

    public async Task<bool> UpdateProductStockAsync(int productId, int newStock)
    {
        try
        {
            var product = await _database.GetByIdAsync<ProductModel>(productId);
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

    public async Task<ProductAnalyticsModel?> GetProductAnalyticsAsync(int productId)
    {
        try
        {
            return await _database.GetByIdAsync<ProductAnalyticsModel>(productId);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting analytics: {productId}");
            return null;
        }
    }

    public async Task<bool> UpdateProductAnalyticsAsync(int productId)
    {
        try
        {
            var analytics = await GetProductAnalyticsAsync(productId);
            if (analytics == null) return false;

            if (analytics.TotalViews > 0)
            {
                analytics.ViewToCartRate = (decimal)analytics.TotalAddToCart / analytics.TotalViews * 100;
                analytics.OverallConversionRate = (decimal)analytics.TotalPurchases / analytics.TotalViews * 100;
            }

            if (analytics.TotalAddToCart > 0)
            {
                analytics.CartToPurchaseRate = (decimal)analytics.TotalPurchases / analytics.TotalAddToCart * 100;
            }

            analytics.UpdatedAt = DateTime.UtcNow;

            var result = await _database.UpdateAsync(analytics);
            return result != null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Updating analytics: {productId}");
            return false;
        }
    }

    public string GenerateUniqueSku()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
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
