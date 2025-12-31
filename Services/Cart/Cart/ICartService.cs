// Services/Cart/ICartService.cs
// Services/Cart/ICartService.cs - UPDATED INTERFACE
using SubashaVentures.Models.Supabase;
using SubashaVentures.Domain.Cart;

namespace SubashaVentures.Services.Cart;

public interface ICartService
{
    /// <summary>
    /// Get all cart items for current user
    /// </summary>
    Task<List<CartModel>> GetUserCartAsync(string userId);
    
    /// <summary>
    /// Get cart summary with totals and pricing
    /// </summary>
    Task<CartSummaryViewModel> GetCartSummaryAsync(string userId);
    
    /// <summary>
    /// Add product to cart
    /// </summary>
    Task<bool> AddToCartAsync(string userId, string productId, int quantity = 1, string? size = null, string? color = null);
    
    /// <summary>
    /// Update cart item quantity
    /// </summary>
    Task<bool> UpdateCartItemQuantityAsync(string userId, string cartItemId, int newQuantity);
    
    /// <summary>
    /// Remove item from cart (with optional size/color specification)
    /// </summary>
    Task<bool> RemoveFromCartAsync(string userId, string productId, string? size = null, string? color = null);
    /// <summary>
    /// Remove item from cart using composite ID (backward compatibility)
    /// </summary>
    Task<bool> RemoveFromCartByIdAsync(string userId, string cartItemId);
    /// <summary>
    /// Clear entire cart
    /// </summary>
    Task<bool> ClearCartAsync(string userId);
    
    /// <summary>
    /// Get cart item count
    /// </summary>
    Task<int> GetCartItemCountAsync(string userId);
    
    /// <summary>
    /// Check if product is in cart
    /// </summary>
    Task<bool> IsInCartAsync(string userId, string productId);
    
    /// <summary>
    /// Validate cart items (check stock, prices)
    /// </summary>
    Task<CartValidationResult> ValidateCartAsync(string userId);
}

public class CartValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<CartItemIssue> ItemIssues { get; set; } = new();
}

public class CartItemIssue
{
    public string CartItemId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty; // OutOfStock, PriceChanged, NoLongerAvailable
    public string Message { get; set; } = string.Empty;
}