// Domain/Payment/PaymentModels.cs
namespace SubashaVentures.Domain.Payment;

/// <summary>
/// Payment request configuration
/// </summary>
public class PaymentRequest
{
    public string Email { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NGN";
    public string Reference { get; set; } = string.Empty;
    public PaymentProvider Provider { get; set; } = PaymentProvider.Paystack;
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    // Order-specific fields
    public string? OrderId { get; set; }
    public string? UserId { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Payment response from gateway
/// </summary>
public class PaymentResponse
{
    public bool Success { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public PaymentProvider Provider { get; set; }
    public bool Cancelled { get; set; }
    public object? RawTransaction { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorDetails { get; set; }
}

/// <summary>
/// Payment verification request
/// </summary>
public class PaymentVerificationRequest
{
    public string Reference { get; set; } = string.Empty;
    public PaymentProvider Provider { get; set; }
}

/// <summary>
/// Payment verification response
/// </summary>
public class PaymentVerificationResponse
{
    public bool Verified { get; set; }
    public string Reference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NGN";
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public PaymentProvider Provider { get; set; }
    public object? RawData { get; set; }
}

/// <summary>
/// Payment provider enum
/// </summary>
public enum PaymentProvider
{
    Paystack,
    Flutterwave
}

/// <summary>
/// Payment configuration from appsettings
/// </summary>
public class PaymentConfiguration
{
    public PaystackConfig Paystack { get; set; } = new();
    public FlutterwaveConfig Flutterwave { get; set; } = new();
    public string DefaultProvider { get; set; } = "paystack";
    public string FallbackProvider { get; set; } = "flutterwave";
    public bool EnableFallback { get; set; } = true;
}

public class PaystackConfig
{
    public string PublicKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool IsTestMode { get; set; } = true;
    public string Currency { get; set; } = "NGN";
}

public class FlutterwaveConfig
{
    public string PublicKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string EncryptionKey { get; set; } = string.Empty;
    public bool IsTestMode { get; set; } = true;
    public string Currency { get; set; } = "NGN";
}
