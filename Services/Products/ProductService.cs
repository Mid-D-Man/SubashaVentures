using SubashaVentures.Domain.Product;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.Categories;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Services.SupaBase;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Postgrest;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Products;

public class ProductService : IProductService
{
    private readonly ISupabaseDatabaseService _database;
    private readonly ICategoryService _categoryService;
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
        ICategoryService categoryService,
        ILogger<ProductService> logger)
    {
        _database = database;
        _categoryService = categoryService;
        _logger = logger;
    }

    public List<string> GetCommonTags() => CommonTags;
    public List<string> GetCommonSizes() => CommonSizes;
    public List<string> GetCommonColors() => CommonColors;

    // ==================== RETRIEVAL ====================

    /// <summary>
    /// Returns all non-deleted, ACTIVE products.
    /// Used by the public-facing shop page — inactive products are excluded.
    /// </summary>
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
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetAllProductsAsync");
            return new List<ProductViewModel>();
        }
    }

    /// <summary>
    /// Returns all non-deleted products regardless of IsActive status.
    /// Used by the admin panel so inactive products are still visible.
    /// </summary>
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
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetProductsAsync");
            return new List<ProductViewModel>();
        }
    }

    public async Task<List<ProductViewModel>> GetProductsByCategoryAsync(string categoryId)
    {
        try
        {
            var products = await _database.GetWithFilterAsync<ProductModel>(
                "category_id", Constants.Operator.Equals, categoryId);
            return products
                .Where(p => !p.IsDeleted && p.IsActive)
                .Select(MapToViewModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"GetProductsByCategoryAsync: {categoryId}");
            return new List<ProductViewModel>();
        }
    }

    public async Task<List<ProductViewModel>> GetProductsByPartnerAsync(Guid partnerId)
    {
        try
        {
            var products = await _database.GetWithFilterAsync<ProductModel>(
                "partner_id", Constants.Operator.Equals, partnerId);
            return products
                .Where(p => !p.IsDeleted && p.IsActive)
                .Select(MapToViewModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"GetProductsByPartnerAsync: {partnerId}");
            return new List<ProductViewModel>();
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
            await MID_HelperFunctions.LogExceptionAsync(ex, $"GetProductByIdAsync: {productId}");
            return null;
        }
    }

    public async Task<ProductViewModel?> GetProductBySkuAsync(string sku)
    {
        try
        {
            var products = await _database.GetWithFilterAsync<ProductModel>(
                "sku", Constants.Operator.Equals, sku);
            var product = products.FirstOrDefault();
            return product != null ? MapToViewModel(product) : null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"GetProductBySkuAsync: {sku}");
            return null;
        }
    }

    // ==================== CREATE ====================

    public async Task<ProductViewModel?> CreateProductAsync(CreateProductRequest request)
    {
        try
        {
            var now = DateTime.UtcNow;

            await MID_HelperFunctions.DebugMessageAsync(
                $"Creating product: Name={request.Name}, SKU={request.Sku}, CategoryId={request.CategoryId}",
                LogLevel.Info);

            var existingSku = await GetProductBySkuAsync(request.Sku);
            if (existingSku != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"SKU already exists: {request.Sku}", LogLevel.Error);
                return null;
            }

            string categoryName = string.Empty;
            if (!string.IsNullOrEmpty(request.CategoryId))
            {
                var category = await _categoryService.GetCategoryByIdAsync(request.CategoryId);
                if (category != null)
                {
                    categoryName = category.Name;
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✓ Category resolved: {request.CategoryId} → {categoryName}", LogLevel.Info);
                }
                else
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"⚠️ Category not found: {request.CategoryId}", LogLevel.Warning);
                }
            }

            var productModel = new ProductModel
            {
                Name              = request.Name,
                Slug              = GenerateSlug(request.Name),
                Description       = request.Description ?? string.Empty,
                LongDescription   = request.LongDescription ?? string.Empty,
                IsOwnedByStore    = request.IsOwnedByStore,
                PartnerId         = request.PartnerId,
                Price             = request.Price,
                OriginalPrice     = request.OriginalPrice,
                IsOnSale          = request.OriginalPrice.HasValue && request.OriginalPrice > request.Price,
                Discount          = CalculateDiscount(request.Price, request.OriginalPrice),
                Images            = request.ImageUrls ?? new List<string>(),
                VideoUrl          = request.VideoUrl,
                Variants          = request.Variants ?? new Dictionary<string, ProductVariant>(),
                BaseWeight        = request.BaseWeight,
                BaseShippingCost  = request.BaseShippingCost,
                HasFreeShipping   = request.HasFreeShipping,
                Sku               = request.Sku,
                CategoryId        = request.CategoryId,
                Category          = categoryName,
                SubCategory       = request.SubCategory,
                Brand             = request.Brand ?? string.Empty,
                Tags              = request.Tags ?? new List<string>(),
                Rating            = 0,
                ReviewCount       = 0,
                IsActive          = true,
                IsFeatured        = request.IsFeatured,
                CreatedAt         = now,
                CreatedBy         = "system",
                IsDeleted         = false
            };

            var result = await _database.InsertAsync(productModel);

            if (result == null || !result.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync("Insert returned null/empty", LogLevel.Error);
                return null;
            }

            var created = result.First();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Product created: ID={created.Id}, Stock={created.Stock}", LogLevel.Info);

            // Create analytics row (best-effort — never blocks product creation)
            try
            {
                var analytics = new ProductAnalyticsModel
                {
                    ProductId              = created.Id,
                    ProductSku             = request.Sku,
                    ProductName            = request.Name,
                    TotalViews             = 0,
                    TotalClicks            = 0,
                    TotalAddToCart         = 0,
                    TotalPurchases         = 0,
                    TotalRevenue           = 0,
                    TotalWishlistAdds      = 0,
                    ViewToCartRate         = 0,
                    CartToPurchaseRate     = 0,
                    OverallConversionRate  = 0,
                    WishlistToCartRate     = 0,
                    WishlistToPurchaseRate = 0,
                    IsTrending             = false,
                    IsBestSeller           = false,
                    NeedsAttention         = false,
                    CreatedAt              = now,
                    UpdatedAt              = now
                };

                await _database.InsertAsync(analytics);

                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Analytics row created for product {created.Id}", LogLevel.Info);
            }
            catch (Exception analyticsEx)
            {
                _logger.LogError(analyticsEx, "Failed to create analytics row for product {Id}", created.Id);
            }

            return MapToViewModel(created);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"CreateProductAsync: {request.Name}");
            return null;
        }
    }

    // ==================== UPDATE ====================

    public async Task<bool> UpdateProductAsync(int productId, UpdateProductRequest request)
    {
        try
        {
            var existing = await _database.GetByIdAsync<ProductModel>(productId);
            if (existing == null)
            {
                _logger.LogWarning("Product not found for update: {Id}", productId);
                return false;
            }

            if (request.Name            != null) existing.Name            = request.Name;
            if (request.Description     != null) existing.Description     = request.Description;
            if (request.LongDescription != null) existing.LongDescription = request.LongDescription;

            if (request.IsOwnedByStore.HasValue) existing.IsOwnedByStore = request.IsOwnedByStore.Value;
            if (request.PartnerId.HasValue)      existing.PartnerId       = request.PartnerId;

            if (request.Price.HasValue)         existing.Price         = request.Price.Value;
            if (request.OriginalPrice.HasValue) existing.OriginalPrice = request.OriginalPrice;

            if (request.ImageUrls != null) existing.Images   = request.ImageUrls;
            if (request.VideoUrl  != null) existing.VideoUrl = request.VideoUrl;

            if (request.Variants != null) existing.Variants = request.Variants;

            if (request.BaseWeight.HasValue)       existing.BaseWeight       = request.BaseWeight.Value;
            if (request.BaseShippingCost.HasValue) existing.BaseShippingCost = request.BaseShippingCost.Value;
            if (request.HasFreeShipping.HasValue)  existing.HasFreeShipping  = request.HasFreeShipping.Value;

            if (request.CategoryId != null && request.CategoryId != existing.CategoryId)
            {
                existing.CategoryId = request.CategoryId;
                var cat = await _categoryService.GetCategoryByIdAsync(request.CategoryId);
                if (cat != null)
                {
                    existing.Category = cat.Name;
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✓ Category updated: {request.CategoryId} → {cat.Name}", LogLevel.Info);
                }
            }

            // SubCategory — null means "leave as is", empty string means "clear it"
            if (request.SubCategory != null)
                existing.SubCategory = string.IsNullOrEmpty(request.SubCategory) ? null : request.SubCategory;

            if (request.Brand != null) existing.Brand = request.Brand;
            if (request.Tags  != null) existing.Tags  = request.Tags;

            if (request.IsFeatured.HasValue) existing.IsFeatured = request.IsFeatured.Value;
            if (request.IsActive.HasValue)   existing.IsActive   = request.IsActive.Value;

            if (request.Price.HasValue || request.OriginalPrice.HasValue)
            {
                existing.IsOnSale = existing.OriginalPrice.HasValue &&
                                    existing.OriginalPrice > existing.Price;
                existing.Discount = CalculateDiscount(existing.Price, existing.OriginalPrice);
            }

            if (request.Name != null)
                existing.Slug = GenerateSlug(request.Name);

            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = "system";

            var result = await _database.UpdateAsync(existing);

            // Update analytics product name if name changed
            if (request.Name != null)
            {
                var analytics = await GetProductAnalyticsByProductIdAsync(productId);
                if (analytics != null)
                {
                    analytics.ProductName = request.Name;
                    analytics.UpdatedAt   = DateTime.UtcNow;
                    await _database.UpdateAsync(analytics);
                }
            }

            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"UpdateProductAsync: {productId}");
            return false;
        }
    }

    // ==================== DELETE ====================

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
            await MID_HelperFunctions.LogExceptionAsync(ex, $"DeleteProductAsync: {productId}");
            return false;
        }
    }

    public async Task<bool> DeleteProductsAsync(List<int> productIds)
    {
        try
        {
            foreach (var id in productIds)
                await DeleteProductAsync(id);
            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "DeleteProductsAsync");
            return false;
        }
    }

    // ==================== STOCK ====================

    public async Task<bool> UpdateProductStockAsync(int productId, int newStock)
    {
        try
        {
            var product = await _database.GetByIdAsync<ProductModel>(productId);
            if (product == null) return false;

            product.Stock     = newStock;
            product.UpdatedAt = DateTime.UtcNow;

            var result = await _database.UpdateAsync(product);
            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"UpdateProductStockAsync: {productId}");
            return false;
        }
    }

    public async Task<bool> UpdateVariantStockAsync(int productId, string variantKey, int newStock)
    {
        try
        {
            var product = await _database.GetByIdAsync<ProductModel>(productId);
            if (product == null) return false;

            if (!product.Variants.TryGetValue(variantKey, out var variant)) return false;

            variant.Stock                = newStock;
            product.Variants[variantKey] = variant;
            product.UpdatedAt            = DateTime.UtcNow;

            var result = await _database.UpdateAsync(product);
            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"UpdateVariantStockAsync: {productId}/{variantKey}");
            return false;
        }
    }

    // ==================== VARIANTS ====================

    public async Task<bool> AddProductVariantAsync(int productId, string variantKey, ProductVariant variant)
    {
        try
        {
            var product = await _database.GetByIdAsync<ProductModel>(productId);
            if (product == null) return false;

            if (product.Variants.ContainsKey(variantKey))
            {
                _logger.LogWarning("Variant {Key} already exists on product {Id}", variantKey, productId);
                return false;
            }

            product.Variants[variantKey] = variant;
            product.UpdatedAt            = DateTime.UtcNow;

            var result = await _database.UpdateAsync(product);
            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"AddProductVariantAsync: {productId}/{variantKey}");
            return false;
        }
    }

    public async Task<bool> UpdateProductVariantAsync(int productId, string variantKey, ProductVariant variant)
    {
        try
        {
            var product = await _database.GetByIdAsync<ProductModel>(productId);
            if (product == null) return false;

            if (!product.Variants.ContainsKey(variantKey))
            {
                _logger.LogWarning("Variant {Key} not found on product {Id}", variantKey, productId);
                return false;
            }

            product.Variants[variantKey] = variant;
            product.UpdatedAt            = DateTime.UtcNow;

            var result = await _database.UpdateAsync(product);
            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"UpdateProductVariantAsync: {productId}/{variantKey}");
            return false;
        }
    }

    public async Task<bool> RemoveProductVariantAsync(int productId, string variantKey)
    {
        try
        {
            var product = await _database.GetByIdAsync<ProductModel>(productId);
            if (product == null) return false;

            if (!product.Variants.Remove(variantKey))
            {
                _logger.LogWarning("Variant {Key} not found on product {Id}", variantKey, productId);
                return false;
            }

            product.UpdatedAt = DateTime.UtcNow;

            var result = await _database.UpdateAsync(product);
            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"RemoveProductVariantAsync: {productId}/{variantKey}");
            return false;
        }
    }

    public async Task<Dictionary<string, ProductVariant>?> GetProductVariantsAsync(int productId)
    {
        try
        {
            var product = await _database.GetByIdAsync<ProductModel>(productId);
            return product?.Variants;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"GetProductVariantsAsync: {productId}");
            return null;
        }
    }

    // ==================== ANALYTICS (ADMIN ONLY) ====================

    private async Task<ProductAnalyticsModel?> GetProductAnalyticsByProductIdAsync(int productId)
    {
        try
        {
            var list = await _database.GetWithFilterAsync<ProductAnalyticsModel>(
                "product_id", Constants.Operator.Equals, productId);
            return list.FirstOrDefault();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"GetProductAnalyticsByProductIdAsync: {productId}");
            return null;
        }
    }

    public async Task<ProductAnalyticsModel?> GetProductAnalyticsAsync(int productId)
        => await GetProductAnalyticsByProductIdAsync(productId);

    public async Task<List<ProductAnalyticsModel>> GetProductAnalyticsBatchAsync(List<int> productIds)
    {
        var result = new List<ProductAnalyticsModel>();
        foreach (var id in productIds)
        {
            var row = await GetProductAnalyticsByProductIdAsync(id);
            if (row != null) result.Add(row);
        }
        return result;
    }

    public async Task<bool> UpdateProductAnalyticsAsync(int productId)
    {
        try
        {
            var analytics = await GetProductAnalyticsByProductIdAsync(productId);
            if (analytics == null) return false;

            if (analytics.TotalViews > 0)
            {
                analytics.ViewToCartRate         = (decimal)analytics.TotalAddToCart / analytics.TotalViews * 100;
                analytics.OverallConversionRate  = (decimal)analytics.TotalPurchases / analytics.TotalViews * 100;
            }
            if (analytics.TotalAddToCart > 0)
                analytics.CartToPurchaseRate     = (decimal)analytics.TotalPurchases / analytics.TotalAddToCart * 100;
            if (analytics.TotalWishlistAdds > 0)
            {
                analytics.WishlistToCartRate     = (decimal)analytics.TotalAddToCart  / analytics.TotalWishlistAdds * 100;
                analytics.WishlistToPurchaseRate = (decimal)analytics.TotalPurchases  / analytics.TotalWishlistAdds * 100;
            }

            analytics.UpdatedAt = DateTime.UtcNow;
            var res = await _database.UpdateAsync(analytics);
            return res != null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"UpdateProductAnalyticsAsync: {productId}");
            return false;
        }
    }

    public async Task<List<ProductVariantAnalyticsModel>> GetVariantAnalyticsAsync(int productId)
    {
        try
        {
            return await _database.GetWithFilterAsync<ProductVariantAnalyticsModel>(
                "product_id", Constants.Operator.Equals, productId);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"GetVariantAnalyticsAsync: {productId}");
            return new List<ProductVariantAnalyticsModel>();
        }
    }

    public async Task<ProductVariantAnalyticsModel?> GetVariantAnalyticsAsync(int productId, string variantKey)
    {
        var all = await GetVariantAnalyticsAsync(productId);
        return all.FirstOrDefault(v => v.VariantKey == variantKey);
    }

    public async Task<List<ProductVariantAnalyticsModel>> GetAllVariantAnalyticsAsync(int skip = 0, int take = 100)
    {
        try
        {
            var all = await _database.GetAllAsync<ProductVariantAnalyticsModel>();
            return all.Skip(skip).Take(take).ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetAllVariantAnalyticsAsync");
            return new List<ProductVariantAnalyticsModel>();
        }
    }

    // ==================== UTILITIES ====================

    public string GenerateUniqueSku()
    {
        var ts     = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var random = Guid.NewGuid().ToString("N")[..4].ToUpper();
        return $"PROD-{ts}-{random}";
    }

    // ==================== PRIVATE HELPERS ====================

    private ProductViewModel MapToViewModel(ProductModel m) => new ProductViewModel
    {
        Id               = m.Id,
        Name             = m.Name,
        Slug             = m.Slug,
        Description      = m.Description,
        LongDescription  = m.LongDescription,
        IsOwnedByStore   = m.IsOwnedByStore,
        PartnerId        = m.PartnerId,
        Price            = m.Price,
        OriginalPrice    = m.OriginalPrice,
        IsOnSale         = m.IsOnSale,
        Discount         = m.Discount,
        Images           = m.Images ?? new List<string>(),
        VideoUrl         = m.VideoUrl,
        Variants         = m.Variants ?? new Dictionary<string, ProductVariant>(),
        Sizes            = m.Sizes   ?? new List<string>(),
        Colors           = m.Colors  ?? new List<string>(),
        Stock            = m.Stock,
        Sku              = m.Sku,
        BaseWeight       = m.BaseWeight,
        BaseShippingCost = m.BaseShippingCost,
        HasFreeShipping  = m.HasFreeShipping,
        CategoryId       = m.CategoryId,
        Category         = m.Category,
        SubCategory      = m.SubCategory,
        Brand            = m.Brand,
        Tags             = m.Tags ?? new List<string>(),
        Rating           = m.Rating,
        ReviewCount      = m.ReviewCount,
        CreatedAt        = m.CreatedAt,
        UpdatedAt        = m.UpdatedAt,
        IsActive         = m.IsActive,
        IsFeatured       = m.IsFeatured
    };

    private static string GenerateSlug(string name) =>
        name.ToLowerInvariant()
            .Replace(" ",  "-")
            .Replace("&",  "and")
            .Replace("'",  "")
            .Replace("\"", "")
            .Replace("/",  "-")
            .Trim('-');

    private static int CalculateDiscount(decimal price, decimal? originalPrice)
    {
        if (!originalPrice.HasValue || originalPrice <= price) return 0;
        return (int)Math.Round((originalPrice.Value - price) / originalPrice.Value * 100);
    }
}
