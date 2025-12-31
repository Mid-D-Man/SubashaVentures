// Domain/User/WishlistItemViewModel.cs - UPDATED FOR JSONB DESIGN
using SubashaVentures.Models.Supabase;

namespace SubashaVentures.Domain.User;

public class WishlistItemViewModel
{
    public string Id { get; set; } = string.Empty; // Composite: userId_productId
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductSlug { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public bool IsOnSale { get; set; }
    public bool IsInStock { get; set; }
    public DateTime AddedAt { get; set; }
    
    public string DisplayPrice => $"₦{Price:N0}";
    
    // ==================== CONVERSION METHODS ====================
    
    /// <summary>
    /// Convert from JSONB WishlistItem to WishlistItemViewModel
    /// Note: This requires product information from the product service
    /// </summary>
    public static WishlistItemViewModel FromWishlistItem(WishlistItem item, string userId, 
        string productName, string productSlug, string imageUrl, decimal price, 
        decimal? originalPrice, bool isOnSale, bool isInStock)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));
            
        // Create composite ID: userId_productId
        var compositeId = $"{userId}_{item.product_id}";
            
        return new WishlistItemViewModel
        {
            Id = compositeId,
            ProductId = item.product_id,
            ProductName = productName,
            ProductSlug = productSlug,
            ImageUrl = imageUrl,
            Price = price,
            OriginalPrice = originalPrice,
            IsOnSale = isOnSale,
            IsInStock = isInStock,
            AddedAt = item.added_at
        };
    }
    
    /// <summary>
    /// Convert from WishlistItemViewModel to JSONB WishlistItem
    /// </summary>
    public WishlistItem ToWishlistItem()
    {
        return new WishlistItem
        {
            product_id = this.ProductId,
            added_at = this.AddedAt
        };
    }
    
    /// <summary>
    /// Parse composite ID to extract components
    /// Format: userId_productId
    /// </summary>
    public static (string userId, string productId) ParseCompositeId(string compositeId)
    {
        var parts = compositeId.Split('_');
        if (parts.Length < 2)
        {
            throw new ArgumentException($"Invalid composite ID format: {compositeId}");
        }
        
        return (parts[0], parts[1]);
    }
}