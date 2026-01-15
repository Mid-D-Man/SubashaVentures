// Utilities/Tracking/ProductViewTracker.cs - ENHANCED WITH DURATION
using Microsoft.JSInterop;
using SubashaVentures.Domain.Product;
using System.Text.Json;

namespace SubashaVentures.Utilities.Tracking;

/// <summary>
/// Helper service to track product views in localStorage for history page with duration tracking
/// </summary>
public class ProductViewTracker
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<ProductViewTracker> _logger;
    
    private const string VIEWED_PRODUCTS_KEY = "viewed_products_history";
    private const int MAX_VIEWED_PRODUCTS = 50;

    public ProductViewTracker(IJSRuntime jsRuntime, ILogger<ProductViewTracker> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>
    /// Track a product view - call this from product detail page
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
            var historyJson = await _jsRuntime.InvokeAsync<string>(
                "localStorage.getItem", 
                VIEWED_PRODUCTS_KEY
            );

            var history = new List<ViewedProduct>();
            
            if (!string.IsNullOrEmpty(historyJson))
            {
                history = JsonSerializer.Deserialize<List<ViewedProduct>>(historyJson) 
                    ?? new List<ViewedProduct>();
            }

            // Remove existing entry for this product
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
            var json = JsonSerializer.Serialize(history);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", VIEWED_PRODUCTS_KEY, json);

            _logger.LogInformation("✅ Tracked view for product: {ProductId} - {ProductName}", 
                product.Id, product.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track product view");
        }
    }

    /// <summary>
    /// Update the duration for a product view
    /// </summary>
    public async Task UpdateViewDurationAsync(string productId, int durationSeconds)
    {
        try
        {
            // Get existing history
            var historyJson = await _jsRuntime.InvokeAsync<string>(
                "localStorage.getItem", 
                VIEWED_PRODUCTS_KEY
            );

            if (string.IsNullOrEmpty(historyJson))
                return;

            var history = JsonSerializer.Deserialize<List<ViewedProduct>>(historyJson);
            
            if (history == null)
                return;

            // Find and update the product
            var product = history.FirstOrDefault(h => h.ProductId == productId);
            
            if (product != null)
            {
                product.DurationSeconds = durationSeconds;

                // Save back to localStorage
                var json = JsonSerializer.Serialize(history);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", VIEWED_PRODUCTS_KEY, json);

                _logger.LogInformation("✅ Updated view duration for product {ProductId}: {Duration}s", 
                    productId, durationSeconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update view duration");
        }
    }

    /// <summary>
    /// Track a product view by ID and details (when full product not available)
    /// </summary>
    public async Task TrackProductViewAsync(
        string productId, 
        string productName, 
        string imageUrl, 
        decimal price)
    {
        try
        {
            // Get existing history
            var historyJson = await _jsRuntime.InvokeAsync<string>(
                "localStorage.getItem", 
                VIEWED_PRODUCTS_KEY
            );

            var history = new List<ViewedProduct>();
            
            if (!string.IsNullOrEmpty(historyJson))
            {
                history = JsonSerializer.Deserialize<List<ViewedProduct>>(historyJson) 
                    ?? new List<ViewedProduct>();
            }

            // Remove existing entry for this product
            history.RemoveAll(h => h.ProductId == productId);

            // Add new entry at the beginning
            history.Insert(0, new ViewedProduct
            {
                ProductId = productId,
                ProductName = productName,
                ImageUrl = imageUrl,
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
            var json = JsonSerializer.Serialize(history);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", VIEWED_PRODUCTS_KEY, json);

            _logger.LogInformation("✅ Tracked view for product: {ProductId}", productId);
        }
        catch (Exception ex)
        {
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
            var historyJson = await _jsRuntime.InvokeAsync<string>(
                "localStorage.getItem", 
                VIEWED_PRODUCTS_KEY
            );

            if (!string.IsNullOrEmpty(historyJson))
            {
                return JsonSerializer.Deserialize<List<ViewedProduct>>(historyJson) 
                    ?? new List<ViewedProduct>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get viewed products");
        }

        return new List<ViewedProduct>();
    }

    /// <summary>
    /// Clear all viewed products
    /// </summary>
    public async Task ClearViewedProductsAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", VIEWED_PRODUCTS_KEY);
            _logger.LogInformation("✅ Cleared viewed products history");
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