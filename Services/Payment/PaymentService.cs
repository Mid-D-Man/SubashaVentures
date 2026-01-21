// Services/Payment/PaymentService.cs
using Microsoft.JSInterop;
using SubashaVentures.Domain.Payment;
using SubashaVentures.Utilities.HelperScripts;
using System.Text.Json;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Payment;

public class PaymentService : IPaymentService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentService> _logger;
    private readonly HttpClient _httpClient;
    private readonly PaymentConfiguration _paymentConfig;

    public PaymentService(
        IJSRuntime jsRuntime,
        IConfiguration configuration,
        ILogger<PaymentService> logger,
        HttpClient httpClient)
    {
        _jsRuntime = jsRuntime;
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;
        
        // Load payment configuration
        _paymentConfig = new PaymentConfiguration();
        configuration.GetSection("Payment").Bind(_paymentConfig);
    }

    public async Task<PaymentResponse> InitializePaymentAsync(PaymentRequest request)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Initializing payment: {request.Amount:C} via {request.Provider}",
                LogLevel.Info
            );

            // Validate request
            if (string.IsNullOrEmpty(request.Email))
            {
                return new PaymentResponse
                {
                    Success = false,
                    Message = "Customer email is required",
                    Provider = request.Provider
                };
            }

            if (request.Amount <= 0)
            {
                return new PaymentResponse
                {
                    Success = false,
                    Message = "Invalid payment amount",
                    Provider = request.Provider
                };
            }

            // Generate reference if not provided
            if (string.IsNullOrEmpty(request.Reference))
            {
                request.Reference = GenerateReference();
            }

            // Initialize payment based on provider
            return request.Provider switch
            {
                PaymentProvider.Paystack => await InitializePaystackAsync(request),
                PaymentProvider.Flutterwave => await InitializeFlutterwaveAsync(request),
                _ => throw new ArgumentException($"Unsupported payment provider: {request.Provider}")
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Initializing payment");
            _logger.LogError(ex, "Payment initialization failed");
            
            return new PaymentResponse
            {
                Success = false,
                Message = $"Payment initialization failed: {ex.Message}",
                Provider = request.Provider,
                ErrorDetails = ex.ToString()
            };
        }
    }

    public async Task<PaymentResponse> InitializePaymentWithFallbackAsync(PaymentRequest request)
    {
        try
        {
            // Try primary provider first
            var primaryProvider = Enum.Parse<PaymentProvider>(_paymentConfig.DefaultProvider, true);
            request.Provider = primaryProvider;
            
            var response = await InitializePaymentAsync(request);
            
            if (response.Success || !_paymentConfig.EnableFallback)
            {
                return response;
            }

            // Try fallback provider
            await MID_HelperFunctions.DebugMessageAsync(
                $"Primary provider failed, trying fallback: {_paymentConfig.FallbackProvider}",
                LogLevel.Warning
            );

            var fallbackProvider = Enum.Parse<PaymentProvider>(_paymentConfig.FallbackProvider, true);
            request.Provider = fallbackProvider;
            request.Reference = GenerateReference(); // Generate new reference for fallback
            
            return await InitializePaymentAsync(request);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Payment with fallback");
            _logger.LogError(ex, "Payment with fallback failed");
            
            return new PaymentResponse
            {
                Success = false,
                Message = "All payment providers failed",
                ErrorDetails = ex.ToString()
            };
        }
    }

    public async Task<PaymentVerificationResponse> VerifyPaymentAsync(PaymentVerificationRequest request)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Verifying payment: {request.Reference} via {request.Provider}",
                LogLevel.Info
            );

            return request.Provider switch
            {
                PaymentProvider.Paystack => await VerifyPaystackPaymentAsync(request.Reference),
                PaymentProvider.Flutterwave => await VerifyFlutterwavePaymentAsync(request.Reference),
                _ => throw new ArgumentException($"Unsupported payment provider: {request.Provider}")
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Verifying payment");
            _logger.LogError(ex, "Payment verification failed");
            
            return new PaymentVerificationResponse
            {
                Verified = false,
                Reference = request.Reference,
                Status = "error",
                Provider = request.Provider
            };
        }
    }

    public string GenerateReference()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var random = new Random().Next(100000, 999999);
        return $"SV-{timestamp}-{random}";
    }

    public PaymentConfiguration GetConfiguration()
    {
        return _paymentConfig;
    }

    public string FormatCurrency(decimal amount, string currency = "NGN")
    {
        return amount.ToString("C0", new System.Globalization.CultureInfo("en-NG"));
    }

    // ==================== PRIVATE METHODS ====================

    private async Task<PaymentResponse> InitializePaystackAsync(PaymentRequest request)
    {
        try
        {
            var config = new
            {
                publicKey = _paymentConfig.Paystack.PublicKey,
                email = request.Email,
                amount = (int)(request.Amount * 100), // Convert to kobo
                reference = request.Reference,
                currency = request.Currency,
                metadata = request.Metadata
            };

            await MID_HelperFunctions.DebugMessageAsync(
                $"Calling Paystack with amount: ₦{request.Amount} (₦{config.amount} kobo)",
                LogLevel.Debug
            );

            var result = await _jsRuntime.InvokeAsync<JsonElement>(
                "paymentHandler.initializePaystack",
                config
            );

            return JsonSerializer.Deserialize<PaymentResponse>(result.GetRawText()) 
                   ?? throw new Exception("Failed to parse Paystack response");
        }
        catch (JSException jsEx)
        {
            await MID_HelperFunctions.LogExceptionAsync(jsEx, "Paystack JS call");
            throw new Exception($"Paystack initialization failed: {jsEx.Message}", jsEx);
        }
    }

    private async Task<PaymentResponse> InitializeFlutterwaveAsync(PaymentRequest request)
    {
        try
        {
            var config = new
            {
                publicKey = _paymentConfig.Flutterwave.PublicKey,
                email = request.Email,
                name = request.CustomerName,
                phone = request.PhoneNumber,
                amount = request.Amount, // Flutterwave uses main currency, not kobo
                reference = request.Reference,
                currency = request.Currency,
                metadata = request.Metadata
            };

            await MID_HelperFunctions.DebugMessageAsync(
                $"Calling Flutterwave with amount: ₦{request.Amount}",
                LogLevel.Debug
            );

            var result = await _jsRuntime.InvokeAsync<JsonElement>(
                "paymentHandler.initializeFlutterwave",
                config
            );

            return JsonSerializer.Deserialize<PaymentResponse>(result.GetRawText()) 
                   ?? throw new Exception("Failed to parse Flutterwave response");
        }
        catch (JSException jsEx)
        {
            await MID_HelperFunctions.LogExceptionAsync(jsEx, "Flutterwave JS call");
            throw new Exception($"Flutterwave initialization failed: {jsEx.Message}", jsEx);
        }
    }

    private async Task<PaymentVerificationResponse> VerifyPaystackPaymentAsync(string reference)
    {
        try
        {
            var url = $"https://api.paystack.co/transaction/verify/{reference}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {_paymentConfig.Paystack.SecretKey}");

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Paystack API error: {content}");
            }

            var jsonDoc = JsonDocument.Parse(content);
            var data = jsonDoc.RootElement.GetProperty("data");
            
            var verified = data.GetProperty("status").GetString() == "success";
            var amount = data.GetProperty("amount").GetInt64() / 100m; // Convert from kobo

            return new PaymentVerificationResponse
            {
                Verified = verified,
                Reference = reference,
                Amount = amount,
                Currency = data.GetProperty("currency").GetString() ?? "NGN",
                Status = data.GetProperty("status").GetString() ?? "unknown",
                PaidAt = data.TryGetProperty("paid_at", out var paidAt) 
                    ? DateTime.Parse(paidAt.GetString() ?? DateTime.UtcNow.ToString()) 
                    : null,
                Channel = data.GetProperty("channel").GetString() ?? "unknown",
                CustomerEmail = data.TryGetProperty("customer", out var customer)
                    ? customer.GetProperty("email").GetString()
                    : null,
                Provider = PaymentProvider.Paystack,
                RawData = data
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Verifying Paystack payment");
            throw;
        }
    }

    private async Task<PaymentVerificationResponse> VerifyFlutterwavePaymentAsync(string reference)
    {
        try
        {
            var url = $"https://api.flutterwave.com/v3/transactions/verify_by_reference?tx_ref={reference}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {_paymentConfig.Flutterwave.SecretKey}");

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Flutterwave API error: {content}");
            }

            var jsonDoc = JsonDocument.Parse(content);
            var data = jsonDoc.RootElement.GetProperty("data");
            
            var verified = data.GetProperty("status").GetString() == "successful";
            var amount = data.GetProperty("amount").GetDecimal();

            return new PaymentVerificationResponse
            {
                Verified = verified,
                Reference = reference,
                Amount = amount,
                Currency = data.GetProperty("currency").GetString() ?? "NGN",
                Status = data.GetProperty("status").GetString() ?? "unknown",
                PaidAt = data.TryGetProperty("created_at", out var createdAt)
                    ? DateTime.Parse(createdAt.GetString() ?? DateTime.UtcNow.ToString())
                    : null,
                Channel = data.GetProperty("payment_type").GetString() ?? "unknown",
                CustomerEmail = data.TryGetProperty("customer", out var customer)
                    ? customer.GetProperty("email").GetString()
                    : null,
                Provider = PaymentProvider.Flutterwave,
                RawData = data
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Verifying Flutterwave payment");
            throw;
        }
    }
}
