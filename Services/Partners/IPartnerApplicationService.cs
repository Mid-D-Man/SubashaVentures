using SubashaVentures.Domain.Partner;
using SubashaVentures.Models.Supabase;

namespace SubashaVentures.Services.Partners;

/// <summary>
/// Service for managing partner application lifecycle.
///
/// User-facing:
///   - Submit application (with location + duplicate + cooldown checks)
///   - View own application status
///   - Check eligibility before showing the apply button
///
/// Admin-facing:
///   - List all applications with filters
///   - Add contact log entries
///   - Change status (under_review / approve / reject)
///   - Approve triggers the edge function which creates the partner record
/// </summary>
public interface IPartnerApplicationService
{
    // ── User Actions ───────────────────────────────────────────

    /// <summary>
    /// Check whether the given user is eligible to submit a new
    /// partner application. Returns a result object with the reason
    /// if they are not eligible.
    /// </summary>
    Task<ApplicationEligibilityResult> CheckEligibilityAsync(string userId);

    /// <summary>
    /// Submit a new partner application on behalf of the given user.
    /// Validates all fields, location, uniqueness, and cooldown before
    /// inserting. Returns the created view model on success.
    /// </summary>
    Task<PartnerApplicationViewModel?> SubmitApplicationAsync(
        string userId,
        SubmitPartnerApplicationRequest request);

    /// <summary>
    /// Get the most recent application for a user.
    /// Returns null if the user has never applied.
    /// </summary>
    Task<PartnerApplicationViewModel?> GetUserApplicationAsync(string userId);

    /// <summary>
    /// Get all applications for a user (full history).
    /// </summary>
    Task<List<PartnerApplicationViewModel>> GetUserApplicationHistoryAsync(string userId);

    // ── Admin Actions ──────────────────────────────────────────

    /// <summary>
    /// Get all applications with optional status filter.
    /// Ordered by submitted_at DESC.
    /// </summary>
    Task<List<PartnerApplicationViewModel>> GetAllApplicationsAsync(
        string? statusFilter = null);

    /// <summary>
    /// Get a single application by ID.
    /// </summary>
    Task<PartnerApplicationViewModel?> GetApplicationByIdAsync(string applicationId);

    /// <summary>
    /// Change the status of an application to "under_review".
    /// Also sets reviewed_by to the admin's user ID.
    /// </summary>
    Task<bool> MarkUnderReviewAsync(string applicationId, string adminUserId);

    /// <summary>
    /// Add a structured contact log entry to an application.
    /// Does not change the application status.
    /// </summary>
    Task<bool> AddContactLogAsync(AddContactLogRequest request);

    /// <summary>
    /// Approve an application.
    /// Calls the approve-partner-application Supabase Edge Function
    /// which atomically:
    ///   1. Creates the partners record
    ///   2. Creates the partner_stores record
    ///   3. Sets user_data.is_partner = true and partner_id
    ///   4. Sends the user a notification
    /// </summary>
    Task<ApplicationApprovalResult> ApproveApplicationAsync(
        string applicationId,
        string adminUserId);

    /// <summary>
    /// Reject an application with a reason.
    /// Applies cooldown logic:
    ///   rejection_count 1 → cooldown 30 days
    ///   rejection_count 2 → cooldown 90 days
    ///   rejection_count 3 → permanently rejected (no reapply ever)
    /// </summary>
    Task<bool> RejectApplicationAsync(
        string applicationId,
        string adminUserId,
        string rejectionReason,
        int? cooldownDaysOverride = null);

    /// <summary>
    /// Get application statistics for the admin dashboard.
    /// </summary>
    Task<ApplicationStatistics> GetApplicationStatisticsAsync();
}

// ── Result / DTO models ────────────────────────────────────────

/// <summary>
/// Result of the eligibility pre-check before showing the apply button.
/// </summary>
public class ApplicationEligibilityResult
{
    public bool IsEligible { get; set; }
    public string? Reason { get; set; }
    public bool IsPermanentlyRejected { get; set; }
    public bool IsAlreadyPartner { get; set; }
    public bool HasActiveApplication { get; set; }
    public bool IsInCooldown { get; set; }
    public DateTime? CooldownUntil { get; set; }

    public string CooldownDisplay
    {
        get
        {
            if (CooldownUntil == null) return string.Empty;
            var ts = CooldownUntil.Value - DateTime.UtcNow;
            if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays} day(s)";
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours} hour(s)";
            return $"{(int)ts.TotalMinutes} minute(s)";
        }
    }

    public static ApplicationEligibilityResult Eligible() =>
        new() { IsEligible = true };

    public static ApplicationEligibilityResult NotEligible(string reason) =>
        new() { IsEligible = false, Reason = reason };
}

/// <summary>
/// Result of the approval operation.
/// </summary>
public class ApplicationApprovalResult
{
    public bool    Success        { get; set; }
    public string? PartnerId      { get; set; }
    public string? PartnerStoreId { get; set; }
    public string? UniquePartnerId { get; set; }
    public string? StoreSlug      { get; set; }
    public string? ErrorMessage   { get; set; }
}

/// <summary>
/// Summary statistics for the admin applications panel.
/// </summary>
public class ApplicationStatistics
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int UnderReview { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int PermanentlyRejected { get; set; }
    public int AbujaApplications { get; set; }
    public int KadunaApplications { get; set; }
}
