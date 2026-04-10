using SubashaVentures.Domain.Partner;

namespace SubashaVentures.Services.Partners;

/// <summary>
/// Service for managing partner stores.
/// Handles store profile reads/updates and
/// dashboard data aggregation.
/// </summary>
public interface IPartnerStoreService
{
    // ── Store Profile ──────────────────────────────────────────

    /// <summary>
    /// Get a partner's store by partner ID.
    /// </summary>
    Task<PartnerStoreViewModel?> GetStoreByPartnerIdAsync(string partnerId);

    /// <summary>
    /// Get a store by its public slug.
    /// Used on the public store page.
    /// </summary>
    Task<PartnerStoreViewModel?> GetStoreBySlugAsync(string slug);

    /// <summary>
    /// Get all active stores. Used for store directory.
    /// </summary>
    Task<List<PartnerStoreViewModel>> GetAllActiveStoresAsync();

    /// <summary>
    /// Update a partner's store display fields.
    /// Partners can only update display fields — slug, is_active,
    /// and partner_id are admin-only (rejected at service layer).
    /// </summary>
    Task<bool> UpdateStoreAsync(string storeId, string partnerId,
        UpdateStoreRequest request);

    // ── Partner Dashboard ──────────────────────────────────────

    /// <summary>
    /// Assemble the full partner dashboard view model.
    /// Aggregates store, template stats, financial summary,
    /// recent activity, and alerts in a single call.
    /// </summary>
    Task<PartnerDashboardViewModel?> GetDashboardAsync(
        string partnerId,
        string userId);

    // ── Financials ─────────────────────────────────────────────

    /// <summary>
    /// Get the full earnings summary for a partner.
    /// </summary>
    Task<PartnerEarningsSummary?> GetEarningsSummaryAsync(string partnerId);

    /// <summary>
    /// Submit a payout request.
    /// Validates: amount >= threshold, no open request,
    /// bank details exist.
    /// </summary>
    Task<PayoutSubmissionResult> RequestPayoutAsync(
        string partnerId,
        string userId,
        RequestPayoutRequest request);

    /// <summary>
    /// Cancel a pending payout request.
    /// Only the partner who made the request can cancel it.
    /// </summary>
    Task<bool> CancelPayoutRequestAsync(
        string payoutRequestId,
        string partnerId);

    /// <summary>
    /// Submit a bank detail update request.
    /// Admin must approve before the change takes effect.
    /// </summary>
    Task<bool> RequestBankUpdateAsync(
        string partnerId,
        string userId,
        RequestBankUpdateRequest request);

    /// <summary>
    /// Get all payout requests for a partner.
    /// </summary>
    Task<List<PartnerPayoutRequestViewModel>> GetPayoutRequestsAsync(
        string partnerId);

    /// <summary>
    /// Get all bank update requests for a partner.
    /// </summary>
    Task<List<PartnerBankUpdateRequestViewModel>> GetBankUpdateRequestsAsync(
        string partnerId);

    // ── Admin: Payout Management ───────────────────────────────

    /// <summary>
    /// Get all payout requests across all partners with optional filter.
    /// </summary>
    Task<List<PartnerPayoutRequestViewModel>> GetAllPayoutRequestsAsync(
        string? statusFilter = null);

    /// <summary>
    /// Mark a payout request as processing.
    /// </summary>
    Task<bool> MarkPayoutProcessingAsync(
        string payoutRequestId,
        string adminUserId);

    /// <summary>
    /// Mark a payout request as paid.
    /// Records transaction reference and updates partner pending_payout.
    /// </summary>
    Task<bool> MarkPayoutPaidAsync(MarkPayoutPaidRequest request);

    /// <summary>
    /// Reject a payout request with a reason.
    /// </summary>
    Task<bool> RejectPayoutRequestAsync(
        string payoutRequestId,
        string adminUserId,
        string reason);

    // ── Admin: Bank Update Management ─────────────────────────

    /// <summary>
    /// Get all pending bank update requests.
    /// </summary>
    Task<List<PartnerBankUpdateRequestViewModel>> GetAllBankUpdateRequestsAsync(
        string? statusFilter = null);

    /// <summary>
    /// Approve a bank detail update request.
    /// Atomically updates partners.bank_details.
    /// </summary>
    Task<bool> ApproveBankUpdateAsync(
        string requestId,
        string adminUserId,
        string? adminNotes = null);

    /// <summary>
    /// Reject a bank detail update request.
    /// </summary>
    Task<bool> RejectBankUpdateAsync(
        string requestId,
        string adminUserId,
        string rejectionReason);
}

/// <summary>
/// Result of submitting a payout request.
/// </summary>
public class PayoutSubmissionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? PayoutRequestId { get; set; }

    public static PayoutSubmissionResult Ok(string requestId) =>
        new() { Success = true, PayoutRequestId = requestId };

    public static PayoutSubmissionResult Fail(string error) =>
        new() { Success = false, ErrorMessage = error };
}
