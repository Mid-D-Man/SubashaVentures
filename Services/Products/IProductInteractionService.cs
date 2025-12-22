// Services/Products/IProductInteractionService.cs
using SubashaVentures.Models.Supabase;

namespace SubashaVentures.Services.Products;

/// <summary>
/// Service for tracking user interactions with products
/// Uses local batching to reduce edge function calls
/// </summary>
public interface IProductInteractionService
{
    /// <summary>
    /// Track product view (batched locally)
    /// </summary>
    Task TrackViewAsync(int productId, string userId);
    
    /// <summary>
    /// Track product click (batched locally)
    /// </summary>
    Task TrackClickAsync(int productId, string userId);
    
    /// <summary>
    /// Track add to cart (batched locally)
    /// </summary>
    Task TrackAddToCartAsync(int productId, string userId);
    
    /// <summary>
    /// Track product purchase (batched locally)
    /// </summary>
    Task TrackPurchaseAsync(int productId, string userId, decimal amount, int quantity);
    
    /// <summary>
    /// Track wishlist add (batched locally)
    /// </summary>
    Task TrackWishlistAsync(int productId, string userId);
    
    /// <summary>
    /// Force flush all pending interactions to edge function
    /// </summary>
    Task FlushPendingInteractionsAsync();
    
    /// <summary>
    /// Get pending interaction count (for debugging)
    /// </summary>
    int GetPendingInteractionCount();
    
    /// <summary>
    /// Start auto-flush timer (flushes every 30 seconds)
    /// </summary>
    void StartAutoFlush();
    
    /// <summary>
    /// Stop auto-flush timer
    /// </summary>
    void StopAutoFlush();
}

/// <summary>
/// Product interaction types
/// </summary>
public enum InteractionType
{
    View,
    Click,
    AddToCart,
    Purchase,
    Wishlist
}

/// <summary>
/// Single product interaction record
/// </summary>
public class ProductInteraction
{
    public int ProductId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public InteractionType Type { get; set; }
    public DateTime Timestamp { get; set; }
    public decimal? Amount { get; set; } // For purchases
    public int? Quantity { get; set; } // For purchases
}

/// <summary>
/// Batched interactions payload for edge function
/// </summary>
public class ProductInteractionBatch
{
    public List<ProductInteraction> Interactions { get; set; } = new();
    public DateTime BatchTimestamp { get; set; }
    public string BatchId { get; set; } = Guid.NewGuid().ToString();
}
