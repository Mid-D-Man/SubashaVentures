using SubashaVentures.Models.Supabase;

namespace SubashaVentures.Domain.Cart;

/// <summary>
/// View model for cart item in shopping cart
/// UPDATED: Added variant key support
/// </summary>
public class CartItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
     public string Sku { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    
    // Pricing
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    
    // Quantity
    public int Quantity { get; set; }
    public int MaxQuantity { get; set; }
    
    // Selected Variants
    public string? Size { get; set; }
    public string? Color { get; set; }
    public string? ColorHex { get; set; }
    
    // Variant Key (for efficient lookups)
    public string? VariantKey { get; set; }
    
    // Stock status
    public int Stock { get; set; }
    public bool IsInStock => Stock > 0;
    
    // Shipping
    public decimal ShippingCost { get; set; }
    public bool HasFreeShipping { get; set; }
    public decimal Weight { get; set; }
    
    // Computed
    public decimal Subtotal => Price * Quantity;
    public string DisplayPrice => $"₦{Price:N0}";
    public string DisplaySubtotal => $"₦{Subtotal:N0}";
    public string DisplayShipping => HasFreeShipping ? "FREE" : $"₦{ShippingCost:N0}";
    
    // Validation
    public bool CanIncreaseQuantity => Quantity < Stock && Quantity < MaxQuantity;
    public bool CanDecreaseQuantity => Quantity > 1;
    
    // Timestamps
    public DateTime AddedAt { get; set; }
    
    // ==================== CONVERSION METHODS ====================
    
    public static CartItemViewModel FromCartItem(CartItem item, string userId, ProductModel product)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));
        if (product == null)
            throw new ArgumentNullException(nameof(product));
            
        // Build variant key if size/color provided
        var variantKey = !string.IsNullOrEmpty(item.size) || !string.IsNullOrEmpty(item.color)
            ? ProductModelExtensions.BuildVariantKey(item.size, item.color)
            : null;
            
        // Get variant-specific data
        var price = product.GetVariantPrice(variantKey);
        var image = product.GetVariantImage(variantKey);
        var stock = product.GetVariantStock(variantKey);
        var shipping = product.GetVariantShippingCost(variantKey);
        var hasFreeShipping = product.VariantHasFreeShipping(variantKey);
        var weight = product.GetVariantWeight(variantKey);
        
        // Get color hex if variant exists
        string? colorHex = null;
        if (!string.IsNullOrEmpty(variantKey) && 
            product.Variants.TryGetValue(variantKey, out var variant))
        {
            colorHex = variant.ColorHex;
        }
            
        // Create composite ID
        var compositeId = $"{userId}_{item.product_id}_{item.size ?? "null"}_{item.color ?? "null"}";
            
        return new CartItemViewModel
        {
            Id = compositeId,
            ProductId = item.product_id,
            Name = product.Name,
            Slug = product.Slug,
            ImageUrl = image,
            Price = price,
            OriginalPrice = product.OriginalPrice,
            Quantity = item.quantity,
            MaxQuantity = stock,
            Size = item.size,
            Color = item.color,
            ColorHex = colorHex,
            VariantKey = variantKey,
            Stock = stock,
            ShippingCost = shipping,
            HasFreeShipping = hasFreeShipping,
            Weight = weight,
            AddedAt = item.added_at,
            Sku = product.Sku
        };
    }
    
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