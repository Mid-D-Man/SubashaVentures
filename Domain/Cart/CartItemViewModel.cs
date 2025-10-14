namespace SubashaVentures.Domain.Cart;

/// <summary>
/// View model for cart item in shopping cart
/// </summary>
public class CartItemViewModel
{
    public string Id { get; set; } = string.Empty;
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
}
