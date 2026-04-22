using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Email;

public class EmailService : IEmailService
{
    private readonly HttpClient _http;
    private readonly ISupabaseAuthService _auth;
    private readonly string _supabaseUrl;
    private readonly string _anonKey;
    private readonly ILogger<EmailService> _logger;

    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    public EmailService(
        HttpClient http,
        ISupabaseAuthService auth,
        IConfiguration config,
        ILogger<EmailService> logger)
    {
        _http        = http;
        _auth        = auth;
        _supabaseUrl = config["Supabase:Url"] ?? string.Empty;
        _anonKey     = config["Supabase:AnonKey"] ?? string.Empty;
        _logger      = logger;
    }

    // ── Partner notification emails ───────────────────────────────────────────

    public async Task SendPartnerApprovedAsync(
        string toEmail,
        string partnerName,
        string businessName,
        string uniquePartnerId,
        string storeSlug)
    {
        var payload = new
        {
            type = "partner_approved",
            to   = toEmail,
            data = new { partnerName, businessName, uniquePartnerId, storeSlug }
        };

        await SendEmailAsync(payload);   // ← fixed: was SendAsync (non-existent)
    }

    public async Task SendPartnerRejectedAsync(
        string toEmail,
        string partnerName,
        string rejectionReason)
    {
        var payload = new
        {
            type = "partner_rejected",
            to   = toEmail,
            data = new { partnerName, rejectionReason }
        };

        await SendEmailAsync(payload);   // ← fixed: was SendAsync (non-existent)
    }

    // ── Newsletter ────────────────────────────────────────────────────────────

    public async Task<EmailResult> SendNewsletterAsync(SendNewsletterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Subject) ||
            string.IsNullOrWhiteSpace(request.HtmlContent))
        {
            return new EmailResult
            {
                Success      = false,
                ErrorMessage = "Subject and content are required."
            };
        }

        var payload = new
        {
            type      = "newsletter",
            sendToAll = true,
            data = new
            {
                subject = request.Subject,
                content = request.HtmlContent,
                ctaText = request.CtaText,
                ctaUrl  = request.CtaUrl,
            }
        };

        return await CallEdgeFunctionAsync(payload);
    }

    // ── Transactional (admin resend) ──────────────────────────────────────────

    public async Task<EmailResult> SendTransactionalAsync(SendTransactionalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.To) ||
            string.IsNullOrWhiteSpace(request.Type))
        {
            return new EmailResult
            {
                Success      = false,
                ErrorMessage = "To and Type are required."
            };
        }

        var payload = new
        {
            type = request.Type,
            to   = request.To,
            data = request.Data,
        };

        return await CallEdgeFunctionAsync(payload);
    }

    // ── Shared HTTP helpers ───────────────────────────────────────────────────

    private async Task<EmailResult> CallEdgeFunctionAsync(object payload)
    {
        try
        {
            var session = await _auth.GetCurrentSessionAsync();
            if (session == null)
                return new EmailResult { Success = false, ErrorMessage = "Not authenticated." };

            var url = $"{_supabaseUrl}/functions/v1/send-email";
            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload, options: _json)
            };
            req.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", session.AccessToken);
            req.Headers.Add("apikey", _anonKey);

            var response = await _http.SendAsync(req);
            var raw      = await response.Content.ReadAsStringAsync();

            await MID_HelperFunctions.DebugMessageAsync(
                $"send-email response ({response.StatusCode}): {raw}", LogLevel.Debug);

            var result = JsonSerializer.Deserialize<EdgeEmailResponse>(raw, _json);

            if (result == null)
                return new EmailResult
                {
                    Success      = false,
                    ErrorMessage = "Empty response from edge function."
                };

            return new EmailResult
            {
                Success      = result.Success,
                Sent         = result.Sent,
                Failed       = result.Failed,
                ErrorMessage = result.Error,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EmailService edge function call failed");
            return new EmailResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Sends an email via the send-email edge function using the service-role key.
    /// Non-fatal — never throws; failures are logged.
    /// </summary>
    private async Task SendEmailAsync(object payload)
    {
        try
        {
            var serviceRoleKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY")
                ?? throw new InvalidOperationException(
                    "SUPABASE_SERVICE_ROLE_KEY environment variable not set");

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_supabaseUrl}/functions/v1/send-email")
            {
                Content = JsonContent.Create(payload, options: _json)
            };

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", serviceRoleKey);
            request.Headers.Add("apikey", _anonKey);

            var response = await _http.SendAsync(request);
            var body     = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("send-email returned {Status}: {Body}",
                    response.StatusCode, body);
            else
                _logger.LogInformation("Email dispatched: {Body}", body);
        }
        catch (Exception ex)
        {
            // Always non-fatal — email must never break business logic
            _logger.LogError(ex, "SendEmailAsync failed for payload type");
        }
    }

    // ── Response shape from edge function ────────────────────────────────────

    private sealed class EdgeEmailResponse
    {
        [JsonPropertyName("success")] public bool    Success { get; set; }
        [JsonPropertyName("sent")]    public int     Sent    { get; set; }
        [JsonPropertyName("failed")]  public int     Failed  { get; set; }
        [JsonPropertyName("error")]   public string? Error   { get; set; }
    }
}
