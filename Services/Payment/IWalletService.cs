// Services/Payment/IWalletService.cs
using SubashaVentures.Domain.Payment;

namespace SubashaVentures.Services.Payment;

/// <summary>
/// Service for managing user wallet operations
/// </summary>
public interface IWalletService
{
    // ==================== WALLET OPERATIONS ====================
    
    /// <summary>
    /// Get user wallet balance
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Wallet view model</returns>
    Task<WalletViewModel?> GetWalletAsync(string userId);
    
    /// <summary>
    /// Create wallet for new user via edge function (auto-called on first access)
    /// This uses Supabase Edge Function to bypass RLS
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Created wallet</returns>
    Task<WalletViewModel?> CreateWalletAsync(string userId);
    
    /// <summary>
    /// Ensure wallet exists (get or create)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Wallet view model</returns>
    Task<WalletViewModel?> EnsureWalletExistsAsync(string userId);
    
    /// <summary>
    /// Top up wallet balance via edge function
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="amount">Amount to credit</param>
    /// <param name="paymentReference">Payment gateway reference</param>
    /// <param name="provider">Payment provider</param>
    /// <returns>Transaction result</returns>
    Task<WalletTransactionViewModel?> TopUpWalletAsync(
        string userId, 
        decimal amount, 
        string paymentReference,
        string provider);
    
    /// <summary>
    /// Deduct from wallet balance via edge function
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="amount">Amount to deduct</param>
    /// <param name="description">Transaction description</param>
    /// <param name="orderId">Optional order ID</param>
    /// <returns>Transaction result</returns>
    Task<WalletTransactionViewModel?> DeductFromWalletAsync(
        string userId, 
        decimal amount, 
        string description,
        string? orderId = null);
    
    /// <summary>
    /// Check if wallet has sufficient balance
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="amount">Amount to check</param>
    /// <returns>True if sufficient, false otherwise</returns>
    Task<bool> HasSufficientBalanceAsync(string userId, decimal amount);
    
    // ==================== TRANSACTION HISTORY ====================
    
    /// <summary>
    /// Get user transaction history
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="skip">Records to skip</param>
    /// <param name="take">Records to take</param>
    /// <returns>List of transactions</returns>
    Task<List<WalletTransactionViewModel>> GetTransactionHistoryAsync(
        string userId, 
        int skip = 0, 
        int take = 20);
    
    /// <summary>
    /// Get transaction by reference
    /// </summary>
    /// <param name="reference">Transaction reference</param>
    /// <returns>Transaction or null</returns>
    Task<WalletTransactionViewModel?> GetTransactionByReferenceAsync(string reference);
    
    // ==================== SAVED CARDS ====================
    
    /// <summary>
    /// Get user's saved payment methods
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of saved cards</returns>
    Task<List<SavedCardViewModel>> GetSavedCardsAsync(string userId);
    
    /// <summary>
    /// Save payment method after verifying with payment gateway via edge function
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="provider">Payment provider</param>
    /// <param name="authorizationCode">Token from provider</param>
    /// <param name="email">User email for verification</param>
    /// <param name="setAsDefault">Set as default card</param>
    /// <returns>Saved card</returns>
    Task<SavedCardViewModel?> SavePaymentMethodAsync(
        string userId,
        string provider,
        string authorizationCode,
        string email,
        bool setAsDefault = false);
    
    /// <summary>
    /// Set default payment method
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="paymentMethodId">Payment method ID</param>
    /// <returns>True if successful</returns>
    Task<bool> SetDefaultPaymentMethodAsync(string userId, string paymentMethodId);
    
    /// <summary>
    /// Delete payment method (soft delete)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="paymentMethodId">Payment method ID</param>
    /// <returns>True if successful</returns>
    Task<bool> DeletePaymentMethodAsync(string userId, string paymentMethodId);
    
    /// <summary>
    /// Get default payment method
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Default card or null</returns>
    Task<SavedCardViewModel?> GetDefaultPaymentMethodAsync(string userId);
}

/// <summary>
/// Card verification response from edge function
/// </summary>
public class CardVerificationResponse
{
    public bool Success { get; set; }
    public bool Verified { get; set; }
    public CardDetails? CardDetails { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
}

/// <summary>
/// Card details from payment gateway
/// </summary>
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

/// <summary>
/// Wallet creation response from edge function
/// </summary>
public class WalletCreationResponse
{
    public bool Success { get; set; }
    public WalletData? Wallet { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
}

/// <summary>
/// Wallet data from edge function
/// </summary>
public class WalletData
{
    public string UserId { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "NGN";
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; }
}