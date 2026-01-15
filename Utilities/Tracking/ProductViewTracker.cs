// Utilities/Tracking/ProductViewTracker.cs - UPDATED TO USE IBlazorAppLocalStorageService
using SubashaVentures.Domain.Product;
using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Utilities.Tracking;

/// <summary>
/// Helper service to track product views in localStorage for history page with duration tracking
/// Uses IBlazorAppLocalStorageService for storage operations
/// </summary>
public class ProductViewTracker
{
    private readonly IBlazorAppLocalStorageService _localStorage;
    private readonly ILogger<ProductViewTracker> _logger;
    
    private const string VIEWED_PRODUCTS_KEY = "viewed_products_history";
    private const int MAX_VIEWED_PRODUCTS = 50;

    public ProductViewTracker(
        IBlazorAppLocalStorageService localStorage, 
        ILogger<ProductViewTracker> logger)
    {
        _localStorage = localStorage;
        _logger = logger;
    }

    /// <summary>
    /// Track a product view - call this when product detail page loads
    /// </summary>
    public async Task TrackProductViewAsync(ProductViewModel product)
    {
        try
        {
            if (product == null)
            {
                _logger.LogWarning("TrackProductView called with null product");
                return;
            }

            // Get existing history
            var history = await _localStorage.GetItemAsync<List<ViewedProduct>>(VIEWED_PRODUCTS_KEY) 
                ?? new List<ViewedProduct>();

            // Remove existing entry for this product (to move it to top)
            history.RemoveAll(h => h.ProductId == product.Id.ToString());

            // Add new entry at the beginning
            history.Insert(0, new ViewedProduct
            {
                ProductId = product.Id.ToString(),
                ProductName = product.Name,
                ImageUrl = product.Images?.FirstOrDefault() ?? "/images/placeholder.jpg",
                Price = product.Price,
                ViewedAt = DateTime.UtcNow,
                DurationSeconds = 0 // Will be updated when user leaves
            });

            // Keep only last MAX_VIEWED_PRODUCTS items
            if (history.Count > MAX_VIEWED_PRODUCTS)
            {
                history = history.Take(MAX_VIEWED_PRODUCTS).ToList();
            }

            // Save back to localStorage
            await _localStorage.SetItemAsync(VIEWED_PRODUCTS_KEY, history);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Tracked view for product: {product.Id} - {product.Name}",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Tracking product view");
            _logger.LogError(ex, "Failed to track product view");
        }
    }

    /// <summary>
    /// Update the duration for a product view - call this when user leaves the page
    /// </summary>
    public async Task UpdateViewDurationAsync(string productId, int durationSeconds)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(productId))
            {
                _logger.LogWarning("UpdateViewDuration called with empty productId");
                return;
            }

            // Get existing history
            var history = await _localStorage.GetItemAsync<List<ViewedProduct>>(VIEWED_PRODUCTS_KEY);
            
            if (history == null || !history.Any())
            {
                _logger.LogWarning("No viewed products history found");
                return;
            }

            // Find and update the product
            var product = history.FirstOrDefault(h => h.ProductId == productId);
            
            if (product != null)
            {
                product.DurationSeconds = durationSeconds;

                // Save back to localStorage
                await _localStorage.SetItemAsync(VIEWED_PRODUCTS_KEY, history);

                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ Updated view duration for product {productId}: {durationSeconds}s",
                    LogLevel.Info
                );
            }
            else
            {
                _logger.LogWarning("Product {ProductId} not found in history", productId);
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Updating view duration for: {productId}");
            _logger.LogError(ex, "Failed to update view duration");
        }
    }

    /// <summary>
    /// Track a product view by individual properties (when full product not available)
    /// </summary>
    public async Task TrackProductViewAsync(
        string productId, 
        string productName, 
        string imageUrl, 
        decimal price)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(productId))
            {
                _logger.LogWarning("TrackProductView called with empty productId");
                return;
            }

            // Get existing history
            var history = await _localStorage.GetItemAsync<List<ViewedProduct>>(VIEWED_PRODUCTS_KEY) 
                ?? new List<ViewedProduct>();

            // Remove existing entry for this product
            history.RemoveAll(h => h.ProductId == productId);

            // Add new entry at the beginning
            history.Insert(0, new ViewedProduct
            {
                ProductId = productId,
                ProductName = productName,
                ImageUrl = imageUrl ?? "/images/placeholder.jpg",
                Price = price,
                ViewedAt = DateTime.UtcNow,
                DurationSeconds = 0
            });

            // Keep only last MAX_VIEWED_PRODUCTS items
            if (history.Count > MAX_VIEWED_PRODUCTS)
            {
                history = history.Take(MAX_VIEWED_PRODUCTS).ToList();
            }

            // Save back to localStorage
            await _localStorage.SetItemAsync(VIEWED_PRODUCTS_KEY, history);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Tracked view for product: {productId}",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Tracking product view");
            _logger.LogError(ex, "Failed to track product view");
        }
    }

    /// <summary>
    /// Get all viewed products
    /// </summary>
    public async Task<List<ViewedProduct>> GetViewedProductsAsync()
    {
        try
        {
            var history = await _localStorage.GetItemAsync<List<ViewedProduct>>(VIEWED_PRODUCTS_KEY) 
                ?? new List<ViewedProduct>();
            
            return history;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get viewed products");
            return new List<ViewedProduct>();
        }
    }

    /// <summary>
    /// Clear all viewed products
    /// </summary>
    public async Task ClearViewedProductsAsync()
    {
        try
        {
            await _localStorage.RemoveItemAsync(VIEWED_PRODUCTS_KEY);
            
            await MID_HelperFunctions.DebugMessageAsync(
                "✅ Cleared viewed products history",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear viewed products");
        }
    }
}

/// <summary>
/// Viewed product entry for localStorage
/// </summary>
public class ViewedProduct
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime ViewedAt { get; set; }
    public int DurationSeconds { get; set; }

    public string ViewedTime
    {
        get
        {
            var span = DateTime.UtcNow - ViewedAt;
            if (span.TotalMinutes < 60)
                return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24)
                return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7)
                return $"{(int)span.TotalDays}d ago";
            return ViewedAt.ToString("MMM dd");
        }
    }

    public string ViewDuration
    {
        get
        {
            if (DurationSeconds < 60)
                return $"{DurationSeconds}s";
            var minutes = DurationSeconds / 60;
            if (minutes < 60)
                return $"{minutes}m";
            var hours = minutes / 60;
            return $"{hours}h {minutes % 60}m";
        }
    }
}