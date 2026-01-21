// Services/Payment/IPaymentService.cs
using SubashaVentures.Domain.Payment;

namespace SubashaVentures.Services.Payment;

/// <summary>
/// Payment service for handling payment gateway integrations
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Initialize payment with specified provider
    /// </summary>
    /// <param name="request">Payment request configuration</param>
    /// <returns>Payment response</returns>
    Task<PaymentResponse> InitializePaymentAsync(PaymentRequest request);
    
    /// <summary>
    /// Initialize payment with automatic fallback
    /// </summary>
    /// <param name="request">Payment request configuration</param>
    /// <returns>Payment response</returns>
    Task<PaymentResponse> InitializePaymentWithFallbackAsync(PaymentRequest request);
    
    /// <summary>
    /// Verify payment transaction
    /// </summary>
    /// <param name="request">Verification request</param>
    /// <returns>Verification response</returns>
    Task<PaymentVerificationResponse> VerifyPaymentAsync(PaymentVerificationRequest request);
    
    /// <summary>
    /// Generate unique payment reference
    /// </summary>
    /// <returns>Unique reference string</returns>
    string GenerateReference();
    
    /// <summary>
    /// Get payment configuration
    /// </summary>
    /// <returns>Payment configuration</returns>
    PaymentConfiguration GetConfiguration();
    
    /// <summary>
    /// Format amount as currency
    /// </summary>
    /// <param name="amount">Amount to format</param>
    /// <param name="currency">Currency code</param>
    /// <returns>Formatted currency string</returns>
    string FormatCurrency(decimal amount, string currency = "NGN");
}
