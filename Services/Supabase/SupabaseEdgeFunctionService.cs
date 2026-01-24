// Services/Supabase/SupabaseEdgeFunctionService.cs - COMPLETE IMPLEMENTATION
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Payment;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Supabase;

public class SupabaseEdgeFunctionService : ISupabaseEdgeFunctionService
{
    private readonly HttpClient _httpClient;
    private readonly ISupabaseAuthService _authService;
    private readonly string _supabaseUrl;
    private readonly string _supabaseAnonKey;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<SupabaseEdgeFunctionService> _logger;

    // Edge function names
    private const string ANALYTICS_FUNCTION = "update-product-analytics";
    private const string SERVER_TIME_FUNCTION = "get-server-time";
    private const string HEALTH_CHECK_FUNCTION = "health-check";
    private const string CREATE_WALLET_FUNCTION = "create-wallet";
    private const string VERIFY_CREDIT_WALLET_FUNCTION = "verify-and-credit-wallet";
    private const string DEDUCT_WALLET_FUNCTION = "deduct-from-wallet";
    private const string GET_CARD_AUTH_FUNCTION = "get-card-authorization";
    private const string VERIFY_CARD_TOKEN_FUNCTION = "verify-card-token";

    public SupabaseEdgeFunctionService(
        HttpClient httpClient,
        ISupabaseAuthService authService,
        IConfiguration configuration,
        ILogger<SupabaseEdgeFunctionService> logger)
    {
        _httpClient = httpClient;
        _authService = authService;
        _supabaseUrl = configuration["Supabase:Url"] ?? "https://wbwmovtewytjibxutssk.supabase.co";
        _supabaseAnonKey = configuration["Supabase:AnonKey"] ?? string.Empty;
        _logger = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };
    }

    // ==================== PRODUCT ANALYTICS ====================

    public async Task<EdgeFunctionResponse<ProductAnalyticsUpdateResult>> UpdateProductAnalyticsAsync(
        ProductInteractionBatch batch)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Sending analytics batch: {batch.Interactions.Count} interactions",
                LogLevel.Info
            );

            var response = await SendEdgeFunctionRequestAsync(ANALYTICS_FUNCTION, batch);
            return await ProcessEdgeFunctionResponse<ProductAnalyticsUpdateResult>(response);
        }
        catch (Exception ex)
        {
            return HandleEdgeFunctionError<ProductAnalyticsUpdateResult>(ex, "Updating product analytics");
        }
    }

    // ==================== SERVER TIME ====================

    public async Task<DateTime> GetServerTimeAsync(string timeType = "utc")
    {
        try
        {
            var response = await SendEdgeFunctionRequestAsync(
                $"{SERVER_TIME_FUNCTION}?type={timeType}",
                null,
                HttpMethod.Get,
                requiresAuth: false
            );

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var timeResponse = JsonSerializer.Deserialize<ServerTimeResponse>(content, _jsonOptions);

                if (timeResponse != null)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✓ Server time retrieved: {timeResponse.Time}",
                        LogLevel.Debug
                    );
                    return timeResponse.Time;
                }
            }

            _logger.LogWarning("Failed to get server time, using local time");
            return DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting server time");
            _logger.LogError(ex, "Failed to get server time, using local time");
            return DateTime.UtcNow;
        }
    }

    // ==================== HEALTH CHECK ====================

    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            var response = await SendEdgeFunctionRequestAsync(
                HEALTH_CHECK_FUNCTION,
                null,
                HttpMethod.Get,
                requiresAuth: false
            );

            var isHealthy = response.IsSuccessStatusCode;

            await MID_HelperFunctions.DebugMessageAsync(
                $"Edge function health check: {(isHealthy ? "✓ Healthy" : "✗ Unhealthy")}",
                isHealthy ? LogLevel.Info : LogLevel.Warning
            );

            return isHealthy;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Edge function health check");
            _logger.LogError(ex, "Edge function health check failed");
            return false;
        }
    }

    // ==================== WALLET OPERATIONS ====================

    public async Task<EdgeFunctionResponse<WalletData>> CreateWalletAsync(string userId)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Creating wallet via edge function for user: {userId}",
                LogLevel.Info
            );

            var payload = new { userId };
            var response = await SendEdgeFunctionRequestAsync(CREATE_WALLET_FUNCTION, payload);
            
            return await ProcessEdgeFunctionResponse<WalletData>(response);
        }
        catch (Exception ex)
        {
            return HandleEdgeFunctionError<WalletData>(ex, "Creating wallet");
        }
    }

    public async Task<EdgeFunctionResponse<WalletCreditResult>> VerifyAndCreditWalletAsync(
        string reference, 
        string provider)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Verifying and crediting wallet: Reference={reference}, Provider={provider}",
                LogLevel.Info
            );

            var payload = new { reference, provider };
            var response = await SendEdgeFunctionRequestAsync(VERIFY_CREDIT_WALLET_FUNCTION, payload);
            
            var result = await ProcessEdgeFunctionResponse<WalletCreditResult>(response);
            
            if (result.Success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ Wallet credited successfully: {result.Data?.Reference}",
                    LogLevel.Info
                );
            }
            
            return result;
        }
        catch (Exception ex)
        {
            return HandleEdgeFunctionError<WalletCreditResult>(ex, "Verifying and crediting wallet");
        }
    }

    public async Task<EdgeFunctionResponse<WalletDeductionResult>> DeductFromWalletAsync(
        string userId,
        decimal amount,
        string description,
        string? orderId = null)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Deducting from wallet: User={userId}, Amount=₦{amount:N0}",
                LogLevel.Info
            );

            var payload = new
            {
                userId,
                amount,
                description,
                orderId,
                metadata = new Dictionary<string, object>
                {
                    { "description", description }
                }
            };

            var response = await SendEdgeFunctionRequestAsync(DEDUCT_WALLET_FUNCTION, payload);
            
            return await ProcessEdgeFunctionResponse<WalletDeductionResult>(response);
        }
        catch (Exception ex)
        {
            return HandleEdgeFunctionError<WalletDeductionResult>(ex, "Deducting from wallet");
        }
    }

    // ==================== PAYMENT METHODS ====================

    public async Task<EdgeFunctionResponse<CardAuthorizationData>> GetCardAuthorizationAsync(
        string reference,
        string email)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Getting card authorization: Reference={reference}",
                LogLevel.Info
            );

            var payload = new { reference, email };
            var response = await SendEdgeFunctionRequestAsync(GET_CARD_AUTH_FUNCTION, payload);
            
            return await ProcessEdgeFunctionResponse<CardAuthorizationData>(response);
        }
        catch (Exception ex)
        {
            return HandleEdgeFunctionError<CardAuthorizationData>(ex, "Getting card authorization");
        }
    }

    public async Task<EdgeFunctionResponse<CardVerificationData>> VerifyCardTokenAsync(
        string userId,
        string provider,
        string authorizationCode,
        string email)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Verifying card token: User={userId}, Provider={provider}",
                LogLevel.Info
            );

            var payload = new { userId, provider, authorizationCode, email };
            var response = await SendEdgeFunctionRequestAsync(VERIFY_CARD_TOKEN_FUNCTION, payload);
            
            return await ProcessEdgeFunctionResponse<CardVerificationData>(response);
        }
        catch (Exception ex)
        {
            return HandleEdgeFunctionError<CardVerificationData>(ex, "Verifying card token");
        }
    }

    // ==================== PRIVATE HELPER METHODS ====================

    private async Task<HttpResponseMessage> SendEdgeFunctionRequestAsync(
        string functionName,
        object? payload = null,
        HttpMethod? method = null,
        bool requiresAuth = true)
    {
        method ??= HttpMethod.Post;

        var requestUrl = $"{_supabaseUrl}/functions/v1/{functionName}";
        var request = new HttpRequestMessage(method, requestUrl);

        // Add authorization header
        if (requiresAuth)
        {
            var session = await _authService.GetCurrentSessionAsync();
            if (session == null || string.IsNullOrEmpty(session.AccessToken))
            {
                throw new Exception("No active session found. Please sign in again.");
            }
            
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        }
        
        // Add API key header
        request.Headers.Add("apikey", _supabaseAnonKey);

        // Add content for POST requests
        if (payload != null && method == HttpMethod.Post)
        {
            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Edge function request payload: {json}",
                LogLevel.Debug
            );

            request.Content = JsonContent.Create(payload, options: _jsonOptions);
        }

        return await _httpClient.SendAsync(request);
    }

    private async Task<EdgeFunctionResponse<T>> ProcessEdgeFunctionResponse<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();

        await MID_HelperFunctions.DebugMessageAsync(
            $"Edge function response ({response.StatusCode}): {content}",
            response.IsSuccessStatusCode ? LogLevel.Debug : LogLevel.Error
        );

        if (!response.IsSuccessStatusCode)
        {
            try
            {
                var errorResponse = JsonSerializer.Deserialize<EdgeFunctionResponse<T>>(content, _jsonOptions);
                if (errorResponse != null)
                {
                    return errorResponse;
                }
            }
            catch { }

            return new EdgeFunctionResponse<T>
            {
                Success = false,
                Message = $"HTTP {response.StatusCode}: {content}",
                ErrorCode = "HTTP_ERROR",
                ErrorDetails = content
            };
        }

        try
        {
            var result = JsonSerializer.Deserialize<EdgeFunctionResponse<T>>(content, _jsonOptions);
            
            if (result != null)
            {
                return result;
            }

            return new EdgeFunctionResponse<T>
            {
                Success = false,
                Message = "Failed to parse edge function response",
                ErrorCode = "PARSE_ERROR"
            };
        }
        catch (JsonException ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Parsing edge function response");
            
            return new EdgeFunctionResponse<T>
            {
                Success = false,
                Message = "Invalid response format from server",
                ErrorCode = "PARSE_ERROR",
                ErrorDetails = ex.ToString()
            };
        }
    }

    private EdgeFunctionResponse<T> HandleEdgeFunctionError<T>(Exception ex, string operation)
    {
        MID_HelperFunctions.LogExceptionAsync(ex, operation).Wait();
        _logger.LogError(ex, "Edge function error: {Operation}", operation);

        return new EdgeFunctionResponse<T>
        {
            Success = false,
            Message = $"An error occurred: {ex.Message}",
            ErrorCode = "UNEXPECTED_ERROR",
            ErrorDetails = ex.ToString()
        };
    }
}
