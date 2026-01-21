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
    /// Create wallet for new user (auto-called on signup)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Created wallet</returns>
    Task<WalletViewModel?> CreateWalletAsync(string userId);
    
    /// <summary>
    /// Top up wallet balance (credit)
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
    /// Deduct from wallet balance (purchase)
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
    /// Refund to wallet balance
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="amount">Amount to refund</param>
    /// <param name="description">Refund reason</param>
    /// <param name="originalReference">Original transaction reference</param>
    /// <returns>Transaction result</returns>
    Task<WalletTransactionViewModel?> RefundToWalletAsync(
        string userId, 
        decimal amount, 
        string description,
        string originalReference);
    
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
    /// Save payment method (card token)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="provider">Payment provider</param>
    /// <param name="authorizationCode">Token from provider</param>
    /// <param name="cardDetails">Card metadata</param>
    /// <param name="setAsDefault">Set as default card</param>
    /// <returns>Saved card</returns>
    Task<SavedCardViewModel?> SavePaymentMethodAsync(
        string userId,
        string provider,
        string authorizationCode,
        CardDetails cardDetails,
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
/// Card details for saving payment method
/// </summary>
public class CardDetails
{
    public string CardType { get; set; } = string.Empty; // visa, mastercard
    public string Last4 { get; set; } = string.Empty;
    public string ExpMonth { get; set; } = string.Empty;
    public string ExpYear { get; set; } = string.Empty;
    public string? Bank { get; set; }
    public string? Brand { get; set; }
}
