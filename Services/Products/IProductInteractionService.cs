// Services/Products/IProductInteractionService.cs - CORRECTED VERSION
using SubashaVentures.Models.Supabase;

namespace SubashaVentures.Services.Products;

/// <summary>
/// Service for tracking user interactions with products
/// Uses local batching to reduce edge function calls
/// 
/// IMPORTANT: Only tracks View and Click events
/// Cart, Wishlist, and Purchase are handled by database triggers automatically
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
    
    // NOTE: AddToCart, Purchase, and Wishlist are NOT tracked here
    // They are automatically handled by database triggers when:
    // - Items are added to cart table
    // - Items are added to wishlist table  
    // - Orders are marked as paid in orders table
    
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
/// Product interaction types (client-side only)
/// </summary>
public enum InteractionType
{
    View,
    Click
    // AddToCart, Purchase, Wishlist removed - handled by DB triggers
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
