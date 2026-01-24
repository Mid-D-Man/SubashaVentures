// Services/Supabase/ISupabaseEdgeFunctionService.cs
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Payment;
using SubashaVentures.Domain.Checkout;

namespace SubashaVentures.Services.Supabase;

public interface ISupabaseEdgeFunctionService
{
    // ==================== PRODUCT ANALYTICS ====================
    
    Task<EdgeFunctionResponse<ProductAnalyticsUpdateResult>> UpdateProductAnalyticsAsync(ProductInteractionBatch batch);
    
    // ==================== SERVER TIME ====================
    
    Task<DateTime> GetServerTimeAsync(string timeType = "utc");
    
    // ==================== HEALTH CHECK ====================
    
    Task<bool> HealthCheckAsync();
    
    // ==================== WALLET OPERATIONS ====================
    
    Task<EdgeFunctionResponse<WalletData>> CreateWalletAsync(string userId);
    Task<EdgeFunctionResponse<WalletCreditResult>> VerifyAndCreditWalletAsync(string reference, string provider);
    Task<EdgeFunctionResponse<WalletDeductionResult>> DeductFromWalletAsync(
        string userId, 
        decimal amount, 
        string description, 
        string? orderId = null);
    
    // ==================== PAYMENT METHODS ====================
    
    Task<EdgeFunctionResponse<CardAuthorizationData>> GetCardAuthorizationAsync(string reference, string email);
    Task<EdgeFunctionResponse<CardVerificationData>> VerifyCardTokenAsync(
        string userId,
        string provider,
        string authorizationCode,
        string email);
    
    // ==================== ORDER CREATION ====================
    
    /// <summary>
    /// Create order via edge function (bypasses RLS, handles transactions)
    /// </summary>
    Task<EdgeFunctionResponse<OrderCreationResult>> CreateOrderAsync(CreateOrderEdgeRequest request);
}

// ==================== ORDER REQUEST/RESPONSE MODELS ====================

public class CreateOrderEdgeRequest
{
    public string UserId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    
    public List<OrderItemEdgeRequest> Items { get; set; } = new();
    
    public decimal Subtotal { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    
    public string ShippingAddressId { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string ShippingMethod { get; set; } = string.Empty;
    
    public string PaymentMethod { get; set; } = string.Empty;
    public string? PaymentReference { get; set; }
}

public class OrderItemEdgeRequest
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string? Size { get; set; }
    public string? Color { get; set; }
}

public class OrderCreationResult
{
    public string OrderId { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

// Keep existing response models...
public class EdgeFunctionResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorDetails { get; set; }
    public bool AlreadyProcessed { get; set; }
}

public class ProductAnalyticsUpdateResult
{
    public int ProcessedCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> UpdatedProducts { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public DateTime ProcessedAt { get; set; }
}

public class WalletData
{
    public string UserId { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "NGN";
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WalletCreditResult
{
    public string TransactionId { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public bool Verified { get; set; }
    public bool Credited { get; set; }
}

public class WalletDeductionResult
{
    public string TransactionId { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
}

public class CardAuthorizationData
{
    public string AuthorizationCode { get; set; } = string.Empty;
    public CardDetails CardDetails { get; set; } = new();
}

public class CardVerificationData
{
    public bool Verified { get; set; }
    public CardDetails CardDetails { get; set; } = new();
}

public class CardDetails
{
    public string CardType { get; set; } = string.Empty;
    public string Last4 { get; set; } = string.Empty;
    public string ExpMonth { get; set; } = string.Empty;
    public string ExpYear { get; set; } = string.Empty;
    public string? Bank { get; set; }
    public string? Brand { get; set; }
    public bool Reusable { get; set; } = true;
}
