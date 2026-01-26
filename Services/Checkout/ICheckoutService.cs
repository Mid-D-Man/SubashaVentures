// Services/Checkout/ICheckoutService.cs
using SubashaVentures.Domain.Checkout;
using SubashaVentures.Domain.Miscellaneous;
using SubashaVentures.Domain.Order;

namespace SubashaVentures.Services.Checkout;

/// <summary>
/// Service for managing checkout process
/// </summary>
public interface ICheckoutService
{
    /// <summary>
    /// Initialize checkout from product page (single item)
    /// </summary>
    Task<CheckoutViewModel?> InitializeFromProductAsync(
        string productId, 
        int quantity, 
        string? size = null, 
        string? color = null);
    
    /// <summary>
    /// Initialize checkout from cart (multiple items)
    /// </summary>
    Task<CheckoutViewModel?> InitializeFromCartAsync(string userId);
    
    /// <summary>
    /// Get available shipping methods with calculated costs
    /// </summary>
    Task<List<ShippingMethodViewModel>> GetShippingMethodsAsync(
        string userId,
        List<CheckoutItemViewModel> items);
    
    /// <summary>
    /// Apply promo code
    /// </summary>
    Task<PromoCodeResult> ApplyPromoCodeAsync(string promoCode, decimal subtotal);
    
    /// <summary>
    /// Validate checkout before placing order
    /// </summary>
    Task<CheckoutValidationResult> ValidateCheckoutAsync(CheckoutViewModel checkout);
    
    /// <summary>
    /// Place order (calls edge function)
    /// Now requires userId parameter
    /// </summary>
    Task<OrderPlacementResult> PlaceOrderAsync(CheckoutViewModel checkout, string userId);
    
    /// <summary>
    /// Process payment and create order
    /// </summary>
    Task<OrderPlacementResult> ProcessPaymentAndCreateOrderAsync(
        string _userId,
        CheckoutViewModel checkout,
        string paymentReference);
}

// ==================== RESULT MODELS ====================

public class PromoCodeResult
{
    public bool IsValid { get; set; }
    public decimal Discount { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
}

public class CheckoutValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class OrderPlacementResult
{
    public bool Success { get; set; }
    public string? OrderId { get; set; }
    public string? OrderNumber { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
}

public enum PaymentStatus
{
    Pending,
    Paid,
    Failed,
    RequiresAction
}
