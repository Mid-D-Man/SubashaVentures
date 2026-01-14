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

    public async Task<List<ProductViewModel>> GetProductsByPartnerAsync(Guid partnerId)
    {
        try
        {
            var products = await _database.GetWithFilterAsync<ProductModel>(
                "partner_id",
                Constants.Operator.Equals,
                partnerId
            );
            
            return products
                .Where(p => !p.IsDeleted && p.IsActive)
                .Select(MapToViewModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting products by partner: {partnerId}");
            return new List<ProductViewModel>();
        }
    }

    public async Task<ProductViewModel?> CreateProductAsync(CreateProductRequest request)
    {
        try
        {
            var now = DateTime.UtcNow;

            await MID_HelperFunctions.DebugMessageAsync(
                $"Creating product: Name={request.Name}, SKU={request.Sku}, CategoryId={request.CategoryId}",
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
                
                // Partnership
                IsOwnedByStore = request.IsOwnedByStore,
                PartnerId = request.PartnerId,
                
                // Pricing
                Price = request.Price,
                OriginalPrice = request.OriginalPrice,
                IsOnSale = request.OriginalPrice.HasValue && request.OriginalPrice > request.Price,
                Discount = CalculateDiscount(request.Price, request.OriginalPrice),
                
                // Media
                Images = request.ImageUrls ?? new List<string>(),
                VideoUrl = request.VideoUrl,
                
                // Variants (ONLY set this - sizes/colors/stock auto-populated)
                Variants = request.Variants ?? new Dictionary<string, ProductVariant>(),
                
                // Shipping
                BaseWeight = request.BaseWeight,
                BaseShippingCost = request.BaseShippingCost,
                HasFreeShipping = request.HasFreeShipping,
                
                // Inventory
                Sku = request.Sku,
                
                // Classification
                CategoryId = request.CategoryId,
                Category = request.Category ?? "",
                SubCategory = request.SubCategory,
                Brand = request.Brand ?? "",
                Tags = request.Tags ?? new List<string>(),
                
                // Initial metrics
                Rating = 0,
                ReviewCount = 0,
                ViewCount = 0,
                ClickCount = 0,
                AddToCartCount = 0,
                PurchaseCount = 0,
                SalesCount = 0,
                TotalRevenue = 0,
                
                // Settings
                IsActive = true,
                IsFeatured = request.IsFeatured,
                
                // Audit
                CreatedAt = now,
                CreatedBy = "system",
                IsDeleted = false
            };

            _logger.LogInformation(
                "Inserting product: {Name}, Partnership: {IsOwnedByStore}/{PartnerId}", 
                request.Name, request.IsOwnedByStore, request.PartnerId
            );

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
                $"✓ Product created with ID: {productId}, Stock: {createdProduct.Stock} (auto-calculated)",
                LogLevel.Info
            );

            // Create analytics row
            try
            {
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

                var analyticsResult = await _database.InsertAsync(analyticsModel);

                if (analyticsResult != null && analyticsResult.Any())
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✓ Analytics created for product ID: {productId}",
                        LogLevel.Info
                    );
                }
            }
            catch (Exception analyticsEx)
            {
                await MID_HelperFunctions.LogExceptionAsync(
                    analyticsEx, 
                    $"Creating analytics for product: {productId}"
                );
                _logger.LogError(
                    analyticsEx, 
                    "Failed to create analytics for product {ProductId}", 
                    productId
                );
            }

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

            // Update fields if provided
            if (request.Name != null) existingProduct.Name = request.Name;
            if (request.Description != null) existingProduct.Description = request.Description;
            if (request.LongDescription != null) existingProduct.LongDescription = request.LongDescription;
            
            // Partnership
            if (request.IsOwnedByStore.HasValue) existingProduct.IsOwnedByStore = request.IsOwnedByStore.Value;
            if (request.PartnerId.HasValue) existingProduct.PartnerId = request.PartnerId;
            
            // Pricing
            if (request.Price.HasValue) existingProduct.Price = request.Price.Value;
            if (request.OriginalPrice.HasValue) existingProduct.OriginalPrice = request.OriginalPrice;
            
            // Media
            if (request.ImageUrls != null) existingProduct.Images = request.ImageUrls;
            if (request.VideoUrl != null) existingProduct.VideoUrl = request.VideoUrl;
            
            // Variants (sizes/colors/stock will auto-update via trigger)
            if (request.Variants != null) existingProduct.Variants = request.Variants;
            
            // Shipping
            if (request.BaseWeight.HasValue) existingProduct.BaseWeight = request.BaseWeight.Value;
            if (request.BaseShippingCost.HasValue) existingProduct.BaseShippingCost = request.BaseShippingCost.Value;
            if (request.HasFreeShipping.HasValue) existingProduct.HasFreeShipping = request.HasFreeShipping.Value;
            
            // Classification
            if (request.CategoryId != null) existingProduct.CategoryId = request.CategoryId;
            if (request.Brand != null) existingProduct.Brand = request.Brand;
            if (request.Tags != null) existingProduct.Tags = request.Tags;
            
            // Settings
            if (request.IsFeatured.HasValue) existingProduct.IsFeatured = request.IsFeatured.Value;
            if (request.IsActive.HasValue) existingProduct.IsActive = request.IsActive.Value;

            // Recalculate sale status
            if (request.Price.HasValue || request.OriginalPrice.HasValue)
            {
                existingProduct.IsOnSale = existingProduct.OriginalPrice.HasValue && 
                                           existingProduct.OriginalPrice > existingProduct.Price;
                existingProduct.Discount = CalculateDiscount(existingProduct.Price, existingProduct.OriginalPrice);
            }
            
            // Update slug if name changed
            if (request.Name != null)
            {
                existingProduct.Slug = GenerateSlug(request.Name);
            }

            existingProduct.UpdatedAt = DateTime.UtcNow;
            existingProduct.UpdatedBy = "system";

            _logger.LogInformation(
                "Updating product {ProductId}: Partnership={IsOwnedByStore}/{PartnerId}",
                productId,
                existingProduct.IsOwnedByStore,
                existingProduct.PartnerId
            );

            var result = await _database.UpdateAsync(existingProduct);
            
            // Update analytics if name changed
            if (request.Name != null)
            {
                var analytics = await GetProductAnalyticsByProductIdAsync(productId);
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

            // Note: If using variants, this should update all variant stocks proportionally
            // or you should use UpdateVariantStockAsync instead
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

    public async Task<bool> UpdateVariantStockAsync(int productId, string variantKey, int newStock)
    {
        try
        {
            var product = await _database.GetByIdAsync<ProductModel>(productId);
            if (product == null) return false;

            if (product.Variants.TryGetValue(variantKey, out var variant))
            {
                variant.Stock = newStock;
                product.Variants[variantKey] = variant;
                product.UpdatedAt = DateTime.UtcNow;

                var result = await _database.UpdateAsync(product);
                return result != null && result.Any();
            }

            return false;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(
                ex, 
                $"Updating variant stock: {productId}/{variantKey}"
            );
            return false;
        }
    }

    public async Task<bool> AddProductVariantAsync(int productId, string variantKey, ProductVariant variant)
    {
        try
        {
            var product = await _database.GetByIdAsync<ProductModel>(productId);
            if (product == null) return false;

            if (product.Variants.ContainsKey(variantKey))
            {
                _logger.LogWarning(
                    "Variant {VariantKey} already exists for product {ProductId}", 
                    variantKey, 
                    productId
                );
                return false;
            }

            product.Variants[variantKey] = variant;
            product.UpdatedAt = DateTime.UtcNow;

            var result = await _database.UpdateAsync(product);
            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(
                ex, 
                $"Adding variant: {productId}/{variantKey}"
            );
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
                _logger.LogWarning(
                    "Variant {VariantKey} not found for product {ProductId}", 
                    variantKey, 
                    productId
                );
                return false;
            }

            product.Variants[variantKey] = variant;
            product.UpdatedAt = DateTime.UtcNow;

            var result = await _database.UpdateAsync(product);
            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(
                ex, 
                $"Updating variant: {productId}/{variantKey}"
            );
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
                _logger.LogWarning(
                    "Variant {VariantKey} not found for product {ProductId}", 
                    variantKey, 
                    productId
                );
                return false;
            }

            product.UpdatedAt = DateTime.UtcNow;

            var result = await _database.UpdateAsync(product);
            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(
                ex, 
                $"Removing variant: {productId}/{variantKey}"
            );
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
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting variants: {productId}");
            return null;
        }
    }

    private async Task<ProductAnalyticsModel?> GetProductAnalyticsByProductIdAsync(int productId)
    {
        try
        {
            var analytics = await _database.GetWithFilterAsync<ProductAnalyticsModel>(
                "product_id",
                Constants.Operator.Equals,
                productId
            );
        
            return analytics.FirstOrDefault();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting analytics by product_id: {productId}");
            return null;
        }
    }

    public async Task<ProductAnalyticsModel?> GetProductAnalyticsAsync(int productId)
    {
        try
        {
            return await GetProductAnalyticsByProductIdAsync(productId);
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
            var analytics = await GetProductAnalyticsByProductIdAsync(productId);
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
            
            // Partnership
            IsOwnedByStore = model.IsOwnedByStore,
            PartnerId = model.PartnerId,
            
            // Pricing
            Price = model.Price,
            OriginalPrice = model.OriginalPrice,
            IsOnSale = model.IsOnSale,
            Discount = model.Discount,
            
            // Media
            Images = model.Images,
            VideoUrl = model.VideoUrl,
            
            // Variants
            Variants = model.Variants,
            Sizes = model.Sizes,
            Colors = model.Colors,
            
            // Inventory
            Stock = model.Stock,
            Sku = model.Sku,
            
            // Shipping
            BaseWeight = model.BaseWeight,
            BaseShippingCost = model.BaseShippingCost,
            HasFreeShipping = model.HasFreeShipping,
            
            // Classification
            CategoryId = model.CategoryId,
            Category = model.Category,
            SubCategory = model.SubCategory,
            Brand = model.Brand,
            Tags = model.Tags,
            
            // Metrics
            Rating = model.Rating,
            ReviewCount = model.ReviewCount,
            ViewCount = model.ViewCount,
            SalesCount = model.SalesCount,
            
            // Metadata
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