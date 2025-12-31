// Domain/Cart/CartItemViewModel.cs - UPDATED FOR JSONB DESIGN
using SubashaVentures.Models.Supabase;

namespace SubashaVentures.Domain.Cart;

/// <summary>
/// View model for cart item in shopping cart
/// </summary>
public class CartItemViewModel
{
    public string Id { get; set; } = string.Empty; // Composite: userId_productId_size_color
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    
    // Pricing
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    
    // Quantity
    public int Quantity { get; set; }
    public int MaxQuantity { get; set; } // Stock limit
    
    // Selected Variants
    public string? Size { get; set; }
    public string? Color { get; set; }
    public string? ColorHex { get; set; }
    
    // Stock status
    public int Stock { get; set; }
    public bool IsInStock => Stock > 0;
    
    // Computed
    public decimal Subtotal => Price * Quantity;
    public string DisplayPrice => $"₦{Price:N0}";
    public string DisplaySubtotal => $"₦{Subtotal:N0}";
    
    // Validation
    public bool CanIncreaseQuantity => Quantity < Stock && Quantity < MaxQuantity;
    public bool CanDecreaseQuantity => Quantity > 1;
    
    // Timestamps
    public DateTime AddedAt { get; set; }
    
    // ==================== CONVERSION METHODS ====================
    
    /// <summary>
    /// Convert from JSONB CartItem to CartItemViewModel
    /// Note: This requires product information from the product service
    /// </summary>
    public static CartItemViewModel FromCartItem(CartItem item, string userId, string productName, 
        string productSlug, string imageUrl, decimal price, decimal? originalPrice, int stock)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));
            
        // Create composite ID: userId_productId_size_color
        var compositeId = $"{userId}_{item.product_id}_{item.size ?? "null"}_{item.color ?? "null"}";
            
        return new CartItemViewModel
        {
            Id = compositeId,
            ProductId = item.product_id,
            Name = productName,
            Slug = productSlug,
            ImageUrl = imageUrl,
            Price = price,
            OriginalPrice = originalPrice,
            Quantity = item.quantity,
            MaxQuantity = stock,
            Size = item.size,
            Color = item.color,
            Stock = stock,
            AddedAt = item.added_at
        };
    }
    
    /// <summary>
    /// Convert from CartItemViewModel to JSONB CartItem
    /// </summary>
    public CartItem ToCartItem()
    {
        return new CartItem
        {
            product_id = this.ProductId,
            quantity = this.Quantity,
            size = this.Size,
            color = this.Color,
            added_at = this.AddedAt
        };
    }
    
    /// <summary>
    /// Parse composite ID to extract components
    /// Format: userId_productId_size_color
    /// </summary>
    public static (string userId, string productId, string? size, string? color) ParseCompositeId(string compositeId)
    {
        var parts = compositeId.Split('_');
        if (parts.Length < 2)
        {
            throw new ArgumentException($"Invalid composite ID format: {compositeId}");
        }
        
        var userId = parts[0];
        var productId = parts[1];
        var size = parts.Length > 2 && parts[2] != "null" ? parts[2] : null;
        var color = parts.Length > 3 && parts[3] != "null" ? parts[3] : null;
        
        return (userId, productId, size, color);
    }
}