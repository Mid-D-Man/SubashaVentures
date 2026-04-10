using SubashaVentures.Models.Supabase;

namespace SubashaVentures.Domain.Partner;

/// <summary>
/// Aggregate view model for the partner dashboard overview page.
/// Assembled by PartnerStoreService from multiple data sources.
/// Not a direct DB model — built at the service layer.
/// </summary>
public class PartnerDashboardViewModel
{
    // ── Identity ──────────────────────────────────────────────
    public string PartnerId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public string UniquePartnerId { get; set; } = string.Empty;
    public string CommissionRate { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = string.Empty;

    // ── Store ─────────────────────────────────────────────────
    public PartnerStoreViewModel? Store { get; set; }
    public bool HasStore => Store != null;

    // ── Product Stats ─────────────────────────────────────────
    public int TotalApprovedProducts { get; set; }
    public int TotalDraftTemplates { get; set; }
    public int TotalPendingTemplates { get; set; }
    public int TotalRejectedTemplates { get; set; }

    // ── Financial Summary ─────────────────────────────────────
    public decimal PendingPayout { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalWithdrawn { get; set; }
    public bool HasOpenPayoutRequest { get; set; }

    // ── Recent Activity ───────────────────────────────────────
    public List<PartnerTemplateViewModel> RecentTemplates { get; set; } = new();
    public List<PartnerPayoutRequestViewModel> RecentPayouts { get; set; } = new();

    // ── Alerts (shown as banners on dashboard) ─────────────────
    /// <summary>
    /// Populated by service layer. Examples:
    /// "You have a template that was rejected — review and resubmit."
    /// "Your payout request is being processed."
    /// "Your bank update request is pending admin approval."
    /// </summary>
    public List<PartnerAlert> Alerts { get; set; } = new();

    // ── Computed Display ───────────────────────────────────────
    public bool CanRequestPayout =>
        PendingPayout >= PayoutConstants.MinimumPayoutAmount
        && !HasOpenPayoutRequest;

    public string DisplayPendingPayout    => $"₦{PendingPayout:N0}";
    public string DisplayTotalRevenue     => $"₦{TotalRevenue:N0}";
    public string DisplayTotalWithdrawn   => $"₦{TotalWithdrawn:N0}";
    public string DisplayCommissionRate   => $"{CommissionRate}%";

    public string VerificationBadgeClass => VerificationStatus switch
    {
        "verified" => "badge-success",
        "pending"  => "badge-warning",
        "rejected" => "badge-danger",
        _          => "badge-secondary"
    };

    public int TotalTemplates =>
        TotalDraftTemplates
        + TotalPendingTemplates
        + TotalRejectedTemplates;
}

/// <summary>
/// A single alert/banner shown on the partner dashboard.
/// </summary>
public class PartnerAlert
{
    public string Message { get; set; } = string.Empty;

    /// <summary>info | warning | danger | success</summary>
    public string Type { get; set; } = "info";

    /// <summary>Optional navigation target, e.g. "user/partner/templates".</summary>
    public string? ActionUrl { get; set; }

    public string? ActionLabel { get; set; }

    public string AlertClass => Type switch
    {
        "info"    => "alert-info",
        "warning" => "alert-warning",
        "danger"  => "alert-danger",
        "success" => "alert-success",
        _         => "alert-info"
    };

    // ── Factories ─────────────────────────────────────────────
    public static PartnerAlert Info(string message, string? actionUrl = null,
        string? actionLabel = null) =>
        new() { Message = message, Type = "info",
                ActionUrl = actionUrl, ActionLabel = actionLabel };

    public static PartnerAlert Warning(string message, string? actionUrl = null,
        string? actionLabel = null) =>
        new() { Message = message, Type = "warning",
                ActionUrl = actionUrl, ActionLabel = actionLabel };

    public static PartnerAlert Danger(string message, string? actionUrl = null,
        string? actionLabel = null) =>
        new() { Message = message, Type = "danger",
                ActionUrl = actionUrl, ActionLabel = actionLabel };

    public static PartnerAlert Success(string message, string? actionUrl = null,
        string? actionLabel = null) =>
        new() { Message = message, Type = "success",
                ActionUrl = actionUrl, ActionLabel = actionLabel };
}
