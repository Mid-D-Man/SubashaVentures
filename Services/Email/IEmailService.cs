// Services/Email/IEmailService.cs
namespace SubashaVentures.Services.Email;

/// <summary>
/// Calls the send-email Supabase Edge Function.
/// Transactional emails (order events) are called by the edge function itself.
/// Use this service for admin-triggered actions: newsletters, manual sends.
/// </summary>
public interface IEmailService
{
    /// <summary>Send newsletter to all subscribed, active users.</summary>
    Task<EmailResult> SendNewsletterAsync(SendNewsletterRequest request);

    /// <summary>
    /// Send a transactional email to a specific address.
    /// Useful for resending order confirmations, etc.
    /// </summary>
    Task<EmailResult> SendTransactionalAsync(SendTransactionalRequest request);
}

// ── Request / Response models ─────────────────────────────────────────────────

public class SendNewsletterRequest
{
    public string Subject { get; set; } = string.Empty;
    /// <summary>HTML content for the newsletter body.</summary>
    public string HtmlContent { get; set; } = string.Empty;
    /// <summary>Optional call-to-action button text.</summary>
    public string? CtaText { get; set; }
    /// <summary>Optional call-to-action URL.</summary>
    public string? CtaUrl { get; set; }
}

public class SendTransactionalRequest
{
    public string To { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;  // order_placed | order_shipped | etc.
    public Dictionary<string, object> Data { get; set; } = new();
}

public class EmailResult
{
    public bool Success { get; set; }
    public int Sent { get; set; }
    public int Failed { get; set; }
    public string? ErrorMessage { get; set; }
}


