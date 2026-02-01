// Services/Products/ProductOfTheDayService.cs
using SubashaVentures.Domain.Product;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.SupaBase;
using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Products;

public class ProductOfTheDayService : IProductOfTheDayService
{
    private readonly IProductService _productService;
    private readonly IBlazorAppLocalStorageService _localStorage;
    private readonly ILogger<ProductOfTheDayService> _logger;
    
    private const string POTD_KEY = "product_of_the_day";
    private const string POTD_DATE_KEY = "product_of_the_day_date";
    private const int CACHE_HOURS = 24;

    public ProductOfTheDayService(
        IProductService productService,
        IBlazorAppLocalStorageService localStorage,
        ILogger<ProductOfTheDayService> logger)
    {
        _productService = productService;
        _localStorage = localStorage;
        _logger = logger;
    }

    public async Task<ProductViewModel?> GetProductOfTheDayAsync()
    {
        try
        {
            // Check if cached product is still valid
            var lastUpdate = await GetLastUpdateTimeAsync();
            
            if (lastUpdate.HasValue && 
                (DateTime.UtcNow - lastUpdate.Value).TotalHours < CACHE_HOURS)
            {
                var cachedProduct = await _localStorage.GetItemAsync<ProductViewModel>(POTD_KEY);
                if (cachedProduct != null)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✓ Returning cached Product of the Day: {cachedProduct.Name}",
                        LogLevel.Info
                    );
                    return cachedProduct;
                }
            }

            // Auto-select new product if cache expired or not found
            await MID_HelperFunctions.DebugMessageAsync(
                "Cache expired or empty, auto-selecting new Product of the Day",
                LogLevel.Info
            );
            
            await AutoSelectProductOfTheDayAsync();
            
            // Return newly selected product
            return await _localStorage.GetItemAsync<ProductViewModel>(POTD_KEY);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting Product of the Day");
            _logger.LogError(ex, "Failed to get Product of the Day");
            return null;
        }
    }

    public async Task<bool> SetProductOfTheDayAsync(int productId)
    {
        try
        {
            var product = await _productService.GetProductByIdAsync(productId);
            
            if (product == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Product not found: {productId}",
                    LogLevel.Error
                );
                return false;
            }

            if (!product.IsActive || product.Stock <= 0)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Product is inactive or out of stock: {product.Name}",
                    LogLevel.Warning
                );
                return false;
            }

            await _localStorage.SetItemAsync(POTD_KEY, product);
            await _localStorage.SetItemAsync(POTD_DATE_KEY, DateTime.UtcNow);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Manually set Product of the Day: {product.Name}",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Setting Product of the Day");
            _logger.LogError(ex, "Failed to set Product of the Day: {ProductId}", productId);
            return false;
        }
    }

    public async Task<bool> AutoSelectProductOfTheDayAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Auto-selecting Product of the Day based on performance metrics",
                LogLevel.Info
            );

            var products = await _productService.GetProductsAsync(0, 100);
            
            if (!products.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No products available for selection",
                    LogLevel.Warning
                );
                return false;
            }

            // Filter active products with stock
            var eligibleProducts = products
                .Where(p => p.IsActive && p.Stock > 0)
                .ToList();

            if (!eligibleProducts.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No eligible products (all inactive or out of stock)",
                    LogLevel.Warning
                );
                return false;
            }

            // Calculate score based on multiple factors
            var scoredProducts = eligibleProducts.Select(p => new
            {
                Product = p,
                Score = CalculateProductScore(p)
            })
            .OrderByDescending(x => x.Score)
            .ToList();

            var winner = scoredProducts.First().Product;

            await _localStorage.SetItemAsync(POTD_KEY, winner);
            await _localStorage.SetItemAsync(POTD_DATE_KEY, DateTime.UtcNow);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Auto-selected Product of the Day: {winner.Name} (Score: {scoredProducts.First().Score:F2})",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Auto-selecting Product of the Day");
            _logger.LogError(ex, "Failed to auto-select Product of the Day");
            return false;
        }
    }

    public async Task<DateTime?> GetLastUpdateTimeAsync()
    {
        try
        {
            return await _localStorage.GetItemAsync<DateTime?>(POTD_DATE_KEY);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get last update time");
            return null;
        }
    }

    /// <summary>
    /// Calculate product score based on multiple performance metrics
    /// Higher score = better candidate for Product of the Day
    /// </summary>
    private decimal CalculateProductScore(ProductViewModel product)
    {
        decimal score = 0;

        // Rating weight (0-5 stars) = 30% of score
        score += (decimal)product.Rating * 6m;

        // Stock availability weight = 15% of score
        // Products with more stock get higher priority
        score += Math.Min(product.Stock / 10m, 15m);

        // Recency bonus = 10% of score
        // Newer products get a boost
        var daysSinceCreation = (DateTime.UtcNow - product.CreatedAt).TotalDays;
        if (daysSinceCreation <= 7)
        {
            score += 10m; // New product bonus
        }
        else if (daysSinceCreation <= 30)
        {
            score += 5m; // Recent product bonus
        }

        // Featured product bonus
        if (product.IsFeatured)
        {
            score += 5m;
        }

        // On sale bonus
        if (product.IsOnSale)
        {
            score += 3m;
        }

        // Has reviews bonus
        if (product.ReviewCount > 0)
        {
            score += 2m;
        }

        return score;
    }
}