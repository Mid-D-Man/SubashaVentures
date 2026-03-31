// Services/Supabase/SupabaseEdgeFunctionService.cs
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SubashaVentures.Domain.Order;
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

    // ── Edge function names ───────────────────────────────────────────────────
    private const string ANALYTICS_FUNCTION             = "update-product-analytics";
    private const string SERVER_TIME_FUNCTION           = "get-server-time";
    private const string HEALTH_CHECK_FUNCTION          = "health-check";
    private const string CREATE_WALLET_FUNCTION         = "create-wallet";
    private const string VERIFY_CREDIT_WALLET_FUNCTION  = "verify-and-credit-wallet";
    private const string DEDUCT_WALLET_FUNCTION         = "deduct-from-wallet";
    private const string GET_CARD_AUTH_FUNCTION         = "get-card-authorization";
    private const string VERIFY_CARD_TOKEN_FUNCTION     = "verify-card-token";
    private const string CREATE_ORDER_FUNCTION          = "create-order";
    private const string GENERATE_COLLECTION_FUNCTION   = "generate-collection-token";
    private const string VALIDATE_COLLECTION_FUNCTION   = "validate-collection-token";

    public SupabaseEdgeFunctionService(
        HttpClient httpClient,
        ISupabaseAuthService authService,
        IConfiguration configuration,
        ILogger<SupabaseEdgeFunctionService> logger)
    {
        _httpClient      = httpClient;
        _authService     = authService;
        _supabaseUrl     = configuration["Supabase:Url"] ?? string.Empty;
        _supabaseAnonKey = configuration["Supabase:AnonKey"] ?? string.Empty;
        _logger          = logger;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
        };
    }

    // ==================== PRODUCT ANALYTICS ====================

    public async Task<EdgeFunctionResponse<ProductAnalyticsUpdateResult>> UpdateProductAnalyticsAsync(
        ProductInteractionBatch batch)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Sending analytics batch: {batch.Interactions.Count} interactions", LogLevel.Info);

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
                requiresAuth: false);

            if (response.IsSuccessStatusCode)
            {
                var content     = await response.Content.ReadAsStringAsync();
                var timeResponse = JsonSerializer.Deserialize<ServerTimeResponse>(content, _jsonOptions);
                if (timeResponse != null)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"Server time retrieved: {timeResponse.Time}", LogLevel.Debug);
                    return timeResponse.Time;
                }
            }

            _logger.LogWarning("Failed to get server time, using local time");
            return DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting server time");
            return DateTime.UtcNow;
        }
    }

    // ==================== HEALTH CHECK ====================

    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            var response = await SendEdgeFunctionRequestAsync(
                HEALTH_CHECK_FUNCTION, null, HttpMethod.Get, requiresAuth: false);

            var isHealthy = response.IsSuccessStatusCode;
            await MID_HelperFunctions.DebugMessageAsync(
                $"Edge function health check: {(isHealthy ? "Healthy" : "Unhealthy")}",
                isHealthy ? LogLevel.Info : LogLevel.Warning);

            return isHealthy;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Edge function health check");
            return false;
        }
    }

    // ==================== ORDER CREATION ====================

    public async Task<EdgeFunctionResponse<OrderCreationResult>> CreateOrderAsync(
        CreateOrderEdgeRequest request)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Creating order via edge function for user: {request.UserId}", LogLevel.Info);

            if (string.IsNullOrWhiteSpace(request.UserId))
                return new EdgeFunctionResponse<OrderCreationResult>
                    { Success = false, Message = "User ID is required", ErrorCode = "INVALID_USER_ID" };

            if (!request.Items.Any())
                return new EdgeFunctionResponse<OrderCreationResult>
                    { Success = false, Message = "Order must contain at least one item", ErrorCode = "NO_ITEMS" };

            var edgeRequest = new
            {
                userId           = request.UserId,
                customerName     = request.CustomerName,
                customerEmail    = request.CustomerEmail,
                customerPhone    = request.CustomerPhone,
                items            = request.Items.Select(i => new
                {
                    productId   = i.ProductId,
                    productName = i.ProductName,
                    productSku  = i.ProductSku,
                    imageUrl    = i.ImageUrl,
                    price       = i.Price,
                    quantity    = i.Quantity,
                    size        = i.Size,
                    color       = i.Color,
                }).ToList(),
                subtotal          = request.Subtotal,
                shippingCost      = request.ShippingCost,
                discount          = request.Discount,
                tax               = request.Tax,
                total             = request.Total,
                shippingAddressId = request.ShippingAddressId,
                shippingAddress   = request.ShippingAddress,
                shippingMethod    = request.ShippingMethod,
                paymentMethod     = request.PaymentMethod,
                paymentReference  = request.PaymentReference,
            };

            var response = await SendEdgeFunctionRequestAsync(CREATE_ORDER_FUNCTION, edgeRequest);
            var raw      = await response.Content.ReadAsStringAsync();

            await MID_HelperFunctions.DebugMessageAsync(
                $"create-order response ({(int)response.StatusCode}): {raw[..Math.Min(raw.Length, 400)]}",
                LogLevel.Info);

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var err = JsonSerializer.Deserialize<EdgeFunctionResponse<OrderCreationResult>>(raw, _jsonOptions);
                    if (err is not null) return err;
                }
                catch { }
                return new EdgeFunctionResponse<OrderCreationResult>
                {
                    Success     = false,
                    Message     = $"HTTP {response.StatusCode}",
                    ErrorCode   = "HTTP_ERROR",
                    ErrorDetails = raw,
                };
            }

            // The create-order function wraps its result in { success, data: { orderId, orderNumber, total, collectionQrUrl, isPickup } }
            try
            {
                var doc     = JsonDocument.Parse(raw).RootElement;
                var success = doc.TryGetProperty("success", out var sv) && sv.GetBoolean();

                if (!success)
                {
                    return new EdgeFunctionResponse<OrderCreationResult>
                    {
                        Success   = false,
                        Message   = doc.TryGetProperty("message", out var mv) ? mv.GetString() ?? "Unknown" : "Unknown",
                        ErrorCode = doc.TryGetProperty("errorCode", out var ev) ? ev.GetString() : null,
                    };
                }

                var data = doc.GetProperty("data");
                return new EdgeFunctionResponse<OrderCreationResult>
                {
                    Success = true,
                    Message = "Order created successfully",
                    Data    = new OrderCreationResult
                    {
                        OrderId         = GetStr(data, "orderId"),
                        OrderNumber     = GetStr(data, "orderNumber"),
                        Total           = data.TryGetProperty("total",           out var t)  ? t.GetDecimal()   : 0m,
                        CollectionQrUrl = data.TryGetProperty("collectionQrUrl", out var qr) ? qr.GetString()   : null,
                        IsPickup        = data.TryGetProperty("isPickup",        out var ip) && ip.GetBoolean(),
                    },
                };
            }
            catch (Exception parseEx)
            {
                _logger.LogError(parseEx, "Failed to parse create-order response");
                return new EdgeFunctionResponse<OrderCreationResult>
                {
                    Success      = false,
                    Message      = "Failed to parse order response",
                    ErrorCode    = "PARSE_ERROR",
                    ErrorDetails = raw,
                };
            }
        }
        catch (Exception ex)
        {
            return HandleEdgeFunctionError<OrderCreationResult>(ex, "Creating order via edge function");
        }
    }

    // ==================== COLLECTION TOKEN ====================

    public async Task<string?> GenerateCollectionTokenAsync(string orderId)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Generating collection token for order: {orderId}", LogLevel.Info);

            var response = await SendEdgeFunctionRequestAsync(
                GENERATE_COLLECTION_FUNCTION, new { orderId });

            var raw = await response.Content.ReadAsStringAsync();

            await MID_HelperFunctions.DebugMessageAsync(
                $"generate-collection-token ({(int)response.StatusCode}): {raw[..Math.Min(raw.Length, 300)]}",
                LogLevel.Info);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("generate-collection-token HTTP {Status}", response.StatusCode);
                return null;
            }

            var doc = JsonDocument.Parse(raw).RootElement;

            if (doc.TryGetProperty("success", out var sv) && sv.GetBoolean()
                && doc.TryGetProperty("qrUrl", out var qv))
            {
                return qv.GetString();
            }

            var errMsg = doc.TryGetProperty("error", out var em) ? em.GetString() : "Unknown error";
            _logger.LogWarning("generate-collection-token failed: {Error}", errMsg);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GenerateCollectionTokenAsync failed for order {OrderId}", orderId);
            return null;
        }
    }

    public async Task<CollectionValidationResult> ValidateCollectionTokenAsync(string t, string s)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Validating collection token via edge function", LogLevel.Info);

            var response = await SendEdgeFunctionRequestAsync(
                VALIDATE_COLLECTION_FUNCTION, new { t, s });

            var raw = await response.Content.ReadAsStringAsync();

            await MID_HelperFunctions.DebugMessageAsync(
                $"validate-collection-token ({(int)response.StatusCode}): {raw[..Math.Min(raw.Length, 400)]}",
                LogLevel.Info);

            if (!response.IsSuccessStatusCode)
            {
                var errBody = TryGetErrorMessage(raw);
                return new CollectionValidationResult { Success = false, Error = errBody };
            }

            var doc     = JsonDocument.Parse(raw).RootElement;
            var success = doc.TryGetProperty("success", out var sv) && sv.GetBoolean();

            if (!success)
            {
                var errMsg = doc.TryGetProperty("error", out var em)
                    ? em.GetString() ?? "Validation failed"
                    : "Validation failed";
                return new CollectionValidationResult { Success = false, Error = errMsg };
            }

            // Parse receipt
            var r       = doc.GetProperty("receipt");
            var receipt = new CollectionReceiptViewModel
            {
                OrderNumber    = GetStr(r, "orderNumber"),
                CustomerName   = GetStr(r, "customerName"),
                CustomerPhone  = GetStr(r, "customerPhone"),
                CustomerEmail  = GetStr(r, "customerEmail"),
                PaymentMethod  = GetStr(r, "paymentMethod"),
                ShippingMethod = GetStr(r, "shippingMethod"),
                Subtotal       = GetDec(r, "subtotal"),
                ShippingCost   = GetDec(r, "shippingCost"),
                Discount       = GetDec(r, "discount"),
                Tax            = GetDec(r, "tax"),
                Total          = GetDec(r, "total"),
                OrderedAt      = r.TryGetProperty("orderedAt",   out var oa) ? oa.GetDateTime()  : default,
                CollectedAt    = r.TryGetProperty("collectedAt", out var ca) ? ca.GetDateTime()  : DateTime.UtcNow,
            };

            if (r.TryGetProperty("items", out var itemsEl))
            {
                foreach (var item in itemsEl.EnumerateArray())
                {
                    receipt.Items.Add(new CollectionReceiptItemViewModel
                    {
                        ProductName = GetStr(item, "product_name"),
                        ProductSku  = GetStr(item, "product_sku"),
                        Quantity    = item.TryGetProperty("quantity", out var q)  ? q.GetInt32()   : 0,
                        Price       = GetDec(item, "price"),
                        Size        = item.TryGetProperty("size",  out var sz)    ? sz.GetString() : null,
                        Color       = item.TryGetProperty("color", out var cl)    ? cl.GetString() : null,
                        Subtotal    = GetDec(item, "subtotal"),
                        ImageUrl    = GetStr(item, "image_url"),
                    });
                }
            }

            return new CollectionValidationResult { Success = true, Receipt = receipt };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ValidateCollectionTokenAsync failed");
            return new CollectionValidationResult
            {
                Success = false,
                Error   = "Failed to validate QR code. Please try again.",
            };
        }
    }

    // ==================== WALLET OPERATIONS ====================

    public async Task<EdgeFunctionResponse<WalletData>> CreateWalletAsync(string userId)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Creating wallet for user: {userId}", LogLevel.Info);

            var response = await SendEdgeFunctionRequestAsync(CREATE_WALLET_FUNCTION, new { userId });
            return await ProcessEdgeFunctionResponse<WalletData>(response);
        }
        catch (Exception ex)
        {
            return HandleEdgeFunctionError<WalletData>(ex, "Creating wallet");
        }
    }

    public async Task<EdgeFunctionResponse<WalletCreditResult>> VerifyAndCreditWalletAsync(
        string reference, string provider)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Verifying and crediting wallet: ref={reference}, provider={provider}", LogLevel.Info);

            var response = await SendEdgeFunctionRequestAsync(
                VERIFY_CREDIT_WALLET_FUNCTION, new { reference, provider });

            var result = await ProcessEdgeFunctionResponse<WalletCreditResult>(response);

            if (result.Success)
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Wallet credited: {result.Data?.Reference}", LogLevel.Info);

            return result;
        }
        catch (Exception ex)
        {
            return HandleEdgeFunctionError<WalletCreditResult>(ex, "Verifying and crediting wallet");
        }
    }

    public async Task<EdgeFunctionResponse<WalletDeductionResult>> DeductFromWalletAsync(
        string userId, decimal amount, string description, string? orderId = null)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Deducting from wallet: user={userId}, amount={amount:N0}", LogLevel.Info);

            var payload = new
            {
                userId,
                amount,
                description,
                orderId,
                metadata = new Dictionary<string, object> { { "description", description } },
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
        string reference, string email)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Getting card authorization: ref={reference}", LogLevel.Info);

            var response = await SendEdgeFunctionRequestAsync(
                GET_CARD_AUTH_FUNCTION, new { reference, email });

            return await ProcessEdgeFunctionResponse<CardAuthorizationData>(response);
        }
        catch (Exception ex)
        {
            return HandleEdgeFunctionError<CardAuthorizationData>(ex, "Getting card authorization");
        }
    }

    public async Task<EdgeFunctionResponse<CardVerificationData>> VerifyCardTokenAsync(
        string userId, string provider, string authorizationCode, string email)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Verifying card token: user={userId}, provider={provider}", LogLevel.Info);

            var payload  = new { userId, provider, authorizationCode, email };
            var response = await SendEdgeFunctionRequestAsync(VERIFY_CARD_TOKEN_FUNCTION, payload);
            return await ProcessEdgeFunctionResponse<CardVerificationData>(response);
        }
        catch (Exception ex)
        {
            return HandleEdgeFunctionError<CardVerificationData>(ex, "Verifying card token");
        }
    }

    // ==================== PRIVATE HELPERS ====================

    private async Task<HttpResponseMessage> SendEdgeFunctionRequestAsync(
        string functionName,
        object? payload     = null,
        HttpMethod? method  = null,
        bool requiresAuth   = true)
    {
        method ??= HttpMethod.Post;

        var requestUrl = $"{_supabaseUrl}/functions/v1/{functionName}";
        var request    = new HttpRequestMessage(method, requestUrl);

        if (requiresAuth)
        {
            var session = await _authService.GetCurrentSessionAsync();
            if (session is null || string.IsNullOrEmpty(session.AccessToken))
                throw new Exception("No active session found. Please sign in again.");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", session.AccessToken);
        }

        request.Headers.Add("apikey", _supabaseAnonKey);

        if (payload is not null && method == HttpMethod.Post)
        {
            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            await MID_HelperFunctions.DebugMessageAsync(
                $"Edge function payload preview: {json[..Math.Min(json.Length, 300)]}", LogLevel.Debug);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return await _httpClient.SendAsync(request);
    }

    private async Task<EdgeFunctionResponse<T>> ProcessEdgeFunctionResponse<T>(
        HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();

        await MID_HelperFunctions.DebugMessageAsync(
            $"Edge function response ({response.StatusCode}): {content[..Math.Min(content.Length, 400)]}",
            response.IsSuccessStatusCode ? LogLevel.Debug : LogLevel.Error);

        if (!response.IsSuccessStatusCode)
        {
            try
            {
                var errResp = JsonSerializer.Deserialize<EdgeFunctionResponse<T>>(content, _jsonOptions);
                if (errResp is not null) return errResp;
            }
            catch { }

            return new EdgeFunctionResponse<T>
            {
                Success      = false,
                Message      = $"HTTP {response.StatusCode}: {content}",
                ErrorCode    = "HTTP_ERROR",
                ErrorDetails = content,
            };
        }

        try
        {
            var result = JsonSerializer.Deserialize<EdgeFunctionResponse<T>>(content, _jsonOptions);
            return result ?? new EdgeFunctionResponse<T>
            {
                Success   = false,
                Message   = "Failed to parse edge function response",
                ErrorCode = "PARSE_ERROR",
            };
        }
        catch (JsonException ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Parsing edge function response");
            return new EdgeFunctionResponse<T>
            {
                Success      = false,
                Message      = "Invalid response format from server",
                ErrorCode    = "PARSE_ERROR",
                ErrorDetails = ex.ToString(),
            };
        }
    }

    private EdgeFunctionResponse<T> HandleEdgeFunctionError<T>(Exception ex, string operation)
    {
        MID_HelperFunctions.LogExceptionAsync(ex, operation).Wait();
        _logger.LogError(ex, "Edge function error in: {Operation}", operation);
        return new EdgeFunctionResponse<T>
        {
            Success      = false,
            Message      = $"An error occurred: {ex.Message}",
            ErrorCode    = "UNEXPECTED_ERROR",
            ErrorDetails = ex.ToString(),
        };
    }

    private static string GetStr(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) ? v.GetString() ?? "" : "";

    private static decimal GetDec(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) ? v.GetDecimal() : 0m;

    private static string TryGetErrorMessage(string raw)
    {
        try
        {
            var doc = JsonDocument.Parse(raw).RootElement;
            if (doc.TryGetProperty("error",   out var e)) return e.GetString() ?? "Unknown error";
            if (doc.TryGetProperty("message", out var m)) return m.GetString() ?? "Unknown error";
        }
        catch { }
        return "Request failed";
    }
}

// ==================== INTERNAL RESPONSE MODEL ====================

public class ServerTimeResponse
{
    [JsonPropertyName("time")]
    public DateTime Time { get; set; }

    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = "UTC";

    [JsonPropertyName("utc_offset")]
    public string UtcOffset { get; set; } = "+00:00";
}
