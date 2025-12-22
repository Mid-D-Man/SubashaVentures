// Services/Supabase/SupabaseEdgeFunctionService.cs
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SubashaVentures.Services.Products;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Supabase;

public class SupabaseEdgeFunctionService : ISupabaseEdgeFunctionService
{
    private readonly HttpClient _httpClient;
    private readonly string _supabaseUrl;
    private readonly string _supabaseKey;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<SupabaseEdgeFunctionService> _logger;

    // Edge function names
    private const string ANALYTICS_FUNCTION = "update-product-analytics";
    private const string SERVER_TIME_FUNCTION = "get-server-time";
    private const string HEALTH_CHECK_FUNCTION = "health-check";

    public SupabaseEdgeFunctionService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SupabaseEdgeFunctionService> logger)
    {
        _httpClient = httpClient;
        _supabaseUrl = configuration["Supabase:Url"] ?? "https://wbwmovtewytjibxutssk.supabase.co";
        _supabaseKey = configuration["Supabase:AnonKey"] ?? string.Empty;
        _logger = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };
    }

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
            var content = await response.Content.ReadAsStringAsync();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Analytics Response Status: {response.StatusCode}",
                LogLevel.Info
            );
            await MID_HelperFunctions.DebugMessageAsync(
                $"Analytics Response Content: {content}",
                LogLevel.Debug
            );

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<EdgeFunctionResponse<ProductAnalyticsUpdateResult>>(
                    content, 
                    _jsonOptions
                );

                if (result != null && result.Success)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✓ Analytics updated successfully: {result.Data?.ProcessedCount} processed",
                        LogLevel.Info
                    );
                    return result;
                }

                return new EdgeFunctionResponse<ProductAnalyticsUpdateResult>
                {
                    Success = false,
                    Message = result?.Message ?? "Unknown error occurred",
                    ErrorCode = result?.ErrorCode ?? "UNKNOWN_ERROR"
                };
            }

            return new EdgeFunctionResponse<ProductAnalyticsUpdateResult>
            {
                Success = false,
                Message = $"HTTP {response.StatusCode}: {content}",
                ErrorCode = "HTTP_ERROR",
                ErrorDetails = content
            };
        }
        catch (HttpRequestException ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "HTTP request to analytics edge function");
            _logger.LogError(ex, "HTTP request failed for analytics update");

            return new EdgeFunctionResponse<ProductAnalyticsUpdateResult>
            {
                Success = false,
                Message = "Network connection failed. Please check your internet connection.",
                ErrorCode = "NETWORK_ERROR",
                ErrorDetails = ex.ToString()
            };
        }
        catch (JsonException ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Parsing analytics response");
            _logger.LogError(ex, "Failed to parse analytics response");

            return new EdgeFunctionResponse<ProductAnalyticsUpdateResult>
            {
                Success = false,
                Message = "Invalid response format from server",
                ErrorCode = "PARSE_ERROR",
                ErrorDetails = ex.ToString()
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Updating product analytics");
            _logger.LogError(ex, "Unexpected error updating product analytics");

            return new EdgeFunctionResponse<ProductAnalyticsUpdateResult>
            {
                Success = false,
                Message = $"An unexpected error occurred: {ex.Message}",
                ErrorCode = "UNKNOWN_ERROR",
                ErrorDetails = ex.ToString()
            };
        }
    }

    public async Task<DateTime> GetServerTimeAsync(string timeType = "utc")
    {
        try
        {
            var response = await SendEdgeFunctionRequestAsync(
                $"{SERVER_TIME_FUNCTION}?type={timeType}",
                null,
                HttpMethod.Get
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

            // Fallback to local time
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

    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            var response = await SendEdgeFunctionRequestAsync(
                HEALTH_CHECK_FUNCTION,
                null,
                HttpMethod.Get
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

    // Private helper methods

    private async Task<HttpResponseMessage> SendEdgeFunctionRequestAsync(
        string functionName,
        object? payload = null,
        HttpMethod? method = null)
    {
        method ??= HttpMethod.Post;

        var requestUrl = $"{_supabaseUrl}/functions/v1/{functionName}";
        var request = new HttpRequestMessage(method, requestUrl);

        // Add authorization header
        if (!string.IsNullOrEmpty(_supabaseKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseKey);
        }

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
}
