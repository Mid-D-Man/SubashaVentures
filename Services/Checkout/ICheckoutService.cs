// Services/Checkout/ICheckoutService.cs
using SubashaVentures.Domain.Checkout;
using SubashaVentures.Domain.Miscellaneous;
using SubashaVentures.Domain.Order;

namespace SubashaVentures.Services.Checkout;

public interface ICheckoutService
{
    Task<CheckoutViewModel?> InitializeFromProductAsync(
        string productId,
        int quantity,
        string? size = null,
        string? color = null);

    Task<CheckoutViewModel?> InitializeFromCartAsync(string userId);

    Task<List<ShippingMethodViewModel>> GetShippingMethodsAsync(
        string userId,
        List<CheckoutItemViewModel> items);

    Task<PromoCodeResult> ApplyPromoCodeAsync(string promoCode, decimal subtotal);

    Task<CheckoutValidationResult> ValidateCheckoutAsync(CheckoutViewModel checkout);

    Task<OrderPlacementResult> PlaceOrderAsync(CheckoutViewModel checkout, string userId);

    Task<OrderPlacementResult> ProcessPaymentAndCreateOrderAsync(
        string userId,
        CheckoutViewModel checkout,
        string paymentReference);
}

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

    // Populated for pickup orders — the QR URL to show the user
    public string? CollectionQrUrl { get; set; }
    public bool IsPickup { get; set; }
}

public enum PaymentStatus
{
    Pending,
    Paid,
    Failed,
    RequiresAction
}
