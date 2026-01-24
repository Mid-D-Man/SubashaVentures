// Services/Supabase/ISupabaseEdgeFunctionService.cs
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Payment;

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
    
    /// <summary>
    /// Create wallet for user (bypasses RLS)
    /// </summary>
    Task<EdgeFunctionResponse<WalletData>> CreateWalletAsync(string userId);
    
    /// <summary>
    /// Verify payment and credit wallet
    /// </summary>
    Task<EdgeFunctionResponse<WalletCreditResult>> VerifyAndCreditWalletAsync(string reference, string provider);
    
    /// <summary>
    /// Deduct from wallet
    /// </summary>
    Task<EdgeFunctionResponse<WalletDeductionResult>> DeductFromWalletAsync(
        string userId, 
        decimal amount, 
        string description, 
        string? orderId = null);
    
    // ==================== PAYMENT METHODS ====================
    
    /// <summary>
    /// Get authorization code from transaction
    /// </summary>
    Task<EdgeFunctionResponse<CardAuthorizationData>> GetCardAuthorizationAsync(string reference, string email);
    
    /// <summary>
    /// Verify card token with payment gateway
    /// </summary>
    Task<EdgeFunctionResponse<CardVerificationData>> VerifyCardTokenAsync(
        string userId,
        string provider,
        string authorizationCode,
        string email);
}

// ==================== RESPONSE MODELS ====================

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

public class ServerTimeResponse
{
    public DateTime Time { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public string FormattedTime { get; set; } = string.Empty;
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
