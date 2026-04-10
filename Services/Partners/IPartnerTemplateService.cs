using SubashaVentures.Domain.Partner;

namespace SubashaVentures.Services.Partners;

/// <summary>
/// Service for managing partner product templates.
///
/// Partner flow:
///   1. Partner creates a draft (SaveDraftAsync)
///   2. Partner uploads images to R2 (handled by CloudflareR2Service)
///   3. Partner submits for review (SubmitForReviewAsync)
///   4. Admin reviews, approves or rejects
///   5. On rejection, partner edits and resubmits
///   6. On approval, edge function converts template to product
///
/// Admin can also bypass this entirely and create partner
/// products directly in the admin panel (no template required).
/// </summary>
public interface IPartnerTemplateService
{
    // ── Partner Actions ────────────────────────────────────────

    /// <summary>
    /// Create a new draft template or update an existing draft/rejected one.
    /// If request.TemplateId is null a new template is created.
    /// Only draft and rejected templates can be edited.
    /// </summary>
    Task<PartnerTemplateViewModel?> SaveDraftAsync(
        string partnerId,
        string userId,
        SaveTemplateRequest request);

    /// <summary>
    /// Submit a draft template for admin review.
    /// Validates: at least 1 image, at least 1 variant, all required fields.
    /// Changes status from draft → pending_review.
    /// </summary>
    Task<TemplateSubmissionResult> SubmitForReviewAsync(
        string templateId,
        string partnerId);

    /// <summary>
    /// Resubmit a previously rejected template after editing.
    /// Changes status from rejected → pending_review.
    /// Increments resubmission_count.
    /// </summary>
    Task<TemplateSubmissionResult> ResubmitAsync(
        string templateId,
        string partnerId);

    /// <summary>
    /// Delete a draft template. Only drafts can be deleted.
    /// Also cleans up associated R2 images.
    /// </summary>
    Task<bool> DeleteDraftAsync(string templateId, string partnerId);

    /// <summary>
    /// Get all templates for a partner with optional status filter.
    /// </summary>
    Task<List<PartnerTemplateViewModel>> GetPartnerTemplatesAsync(
        string partnerId,
        string? statusFilter = null);

    /// <summary>
    /// Get a single template by ID.
    /// Partner can only see their own templates.
    /// </summary>
    Task<PartnerTemplateViewModel?> GetTemplateByIdAsync(
        string templateId,
        string? partnerId = null);

    // ── Admin Actions ──────────────────────────────────────────

    /// <summary>
    /// Get all templates pending admin review.
    /// Ordered by submitted_at ASC (oldest first).
    /// </summary>
    Task<List<PartnerTemplateViewModel>> GetPendingReviewQueueAsync();

    /// <summary>
    /// Get all templates across all partners with optional filter.
    /// Admin only.
    /// </summary>
    Task<List<PartnerTemplateViewModel>> GetAllTemplatesAsync(
        string? statusFilter = null);

    /// <summary>
    /// Approve a template.
    /// Calls the approve-product-template edge function which:
    ///   1. Converts the simplified variant array to full ProductVariant JSONB
    ///   2. Creates a row in the products table
    ///   3. Updates template status to approved and sets approved_product_id
    ///   4. Increments partner_stores.total_products
    ///   5. Notifies the partner
    /// </summary>
    Task<TemplateApprovalResult> ApproveTemplateAsync(ApproveTemplateRequest request);

    /// <summary>
    /// Reject a template with a reason and optional field flags.
    /// Partner can edit and resubmit after rejection.
    /// </summary>
    Task<bool> RejectTemplateAsync(RejectTemplateRequest request);

    /// <summary>
    /// Get template statistics for the admin dashboard.
    /// </summary>
    Task<TemplateStatistics> GetTemplateStatisticsAsync();
}

// ── Result / DTO models ────────────────────────────────────────

/// <summary>
/// Result of submitting or resubmitting a template.
/// </summary>
public class TemplateSubmissionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> ValidationErrors { get; set; } = new();

    public static TemplateSubmissionResult Ok() =>
        new() { Success = true };

    public static TemplateSubmissionResult Fail(string error) =>
        new() { Success = false, ErrorMessage = error };

    public static TemplateSubmissionResult ValidationFail(
        IEnumerable<string> errors) =>
        new()
        {
            Success = false,
            ValidationErrors = errors.ToList(),
            ErrorMessage = "Validation failed"
        };
}

/// <summary>
/// Result of the admin template approval operation.
/// </summary>
public class TemplateApprovalResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The products.id of the newly created product row.
    /// </summary>
    public int? ApprovedProductId { get; set; }
}

/// <summary>
/// Template statistics for admin dashboard.
/// </summary>
public class TemplateStatistics
{
    public int TotalTemplates { get; set; }
    public int PendingReview { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int Drafts { get; set; }
}
