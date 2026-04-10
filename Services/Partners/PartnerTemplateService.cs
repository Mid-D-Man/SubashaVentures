using System.Net.Http.Headers;
using System.Text.Json;
using SubashaVentures.Domain.Partner;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Services.Storage;
using SubashaVentures.Services.SupaBase;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Postgrest;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Partners;

public class PartnerTemplateService : IPartnerTemplateService
{
    private readonly ISupabaseDatabaseService _database;
    private readonly ISupabaseAuthService _auth;
    private readonly ICloudflareR2Service _r2;
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<PartnerTemplateService> _logger;

    public PartnerTemplateService(
        ISupabaseDatabaseService database,
        ISupabaseAuthService auth,
        ICloudflareR2Service r2,
        HttpClient http,
        IConfiguration config,
        ILogger<PartnerTemplateService> logger)
    {
        _database = database;
        _auth     = auth;
        _r2       = r2;
        _http     = http;
        _config   = config;
        _logger   = logger;
    }

    // ── Partner Actions ────────────────────────────────────────

    public async Task<PartnerTemplateViewModel?> SaveDraftAsync(
        string partnerId,
        string userId,
        SaveTemplateRequest request)
    {
        try
        {
            if (!Guid.TryParse(partnerId, out var partnerGuid) ||
                !Guid.TryParse(userId, out var userGuid))
                return null;

            PartnerProductTemplateModel model;
            bool isNew = string.IsNullOrEmpty(request.TemplateId);

            if (!isNew)
            {
                // ── Update existing draft or rejected template ─
                var existing = await GetRawTemplateAsync(request.TemplateId!);

                if (existing == null)
                {
                    _logger.LogWarning(
                        "Template not found for save: {Id}", request.TemplateId);
                    return null;
                }

                if (existing.PartnerId != partnerGuid)
                {
                    _logger.LogWarning(
                        "Partner {PartnerId} attempted to edit template {TemplateId} " +
                        "owned by {OwnerId}",
                        partnerId, request.TemplateId, existing.PartnerId);
                    return null;
                }

                if (!PartnerTemplateStatus.Editable.Contains(existing.Status))
                {
                    _logger.LogWarning(
                        "Cannot edit template in status: {Status}", existing.Status);
                    return null;
                }

                model = existing;
            }
            else
            {
                // ── New template ──────────────────────────────
                model = new PartnerProductTemplateModel
                {
                    Id        = Guid.NewGuid(),
                    PartnerId = partnerGuid,
                    UserId    = userGuid,
                    Status    = PartnerTemplateStatus.Draft,
                    CreatedAt = DateTime.UtcNow,
                    RejectionHistory   = new List<TemplateRejectionEntry>(),
                    ResubmissionCount  = 0
                };
            }

            // ── Apply fields ──────────────────────────────────
            model.TemplateName          = request.TemplateName.Trim();
            model.ProductName           = request.ProductName.Trim();
            model.Description           = request.Description.Trim();
            model.CategoryId            = request.CategoryId;
            model.CategoryName          = request.CategoryName;
            model.ProposedPrice         = request.ProposedPrice;
            model.ProposedOriginalPrice = request.ProposedOriginalPrice;
            model.WeightKg              = request.WeightKg;
            model.HasFreeShipping       = request.HasFreeShipping;
            model.ImageUrls             = request.ImageUrls ?? new List<string>();
            model.Tags                  = request.Tags ?? new List<string>();

            model.Variants = request.Variants.Select(v => new PartnerTemplateVariant
            {
                Sku             = v.Sku.Trim(),
                Size            = string.IsNullOrWhiteSpace(v.Size) ? null : v.Size.Trim(),
                Color           = string.IsNullOrWhiteSpace(v.Color) ? null : v.Color.Trim(),
                Stock           = Math.Max(0, v.Stock),
                PriceAdjustment = v.PriceAdjustment
            }).ToList();

            List<PartnerProductTemplateModel> result;

            if (isNew)
                result = await _database.InsertAsync(model);
            else
                result = await _database.UpdateAsync(model);

            if (result == null || !result.Any()) return null;

            await MID_HelperFunctions.DebugMessageAsync(
                $"Template saved: {result.First().Id} ({(isNew ? "new" : "updated")})",
                LogLevel.Info);

            return PartnerTemplateViewModel.FromCloudModel(result.First());
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "SaveDraft");
            return null;
        }
    }

    public async Task<TemplateSubmissionResult> SubmitForReviewAsync(
        string templateId,
        string partnerId)
    {
        try
        {
            var template = await GetRawTemplateAsync(templateId);

            if (template == null)
                return TemplateSubmissionResult.Fail("Template not found");

            if (!Guid.TryParse(partnerId, out var partnerGuid) ||
                template.PartnerId != partnerGuid)
                return TemplateSubmissionResult.Fail("Access denied");

            if (template.Status != PartnerTemplateStatus.Draft)
                return TemplateSubmissionResult.Fail(
                    $"Only draft templates can be submitted. " +
                    $"Current status: {template.Status}");

            var validationErrors = ValidateTemplateForSubmission(template);
            if (validationErrors.Any())
                return TemplateSubmissionResult.ValidationFail(validationErrors);

            template.Status      = PartnerTemplateStatus.PendingReview;
            template.SubmittedAt = DateTime.UtcNow;

            var result = await _database.UpdateAsync(template);

            if (result == null || !result.Any())
                return TemplateSubmissionResult.Fail("Failed to update template status");

            await MID_HelperFunctions.DebugMessageAsync(
                $"Template submitted for review: {templateId}", LogLevel.Info);

            return TemplateSubmissionResult.Ok();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "SubmitForReview");
            return TemplateSubmissionResult.Fail(ex.Message);
        }
    }

    public async Task<TemplateSubmissionResult> ResubmitAsync(
        string templateId,
        string partnerId)
    {
        try
        {
            var template = await GetRawTemplateAsync(templateId);

            if (template == null)
                return TemplateSubmissionResult.Fail("Template not found");

            if (!Guid.TryParse(partnerId, out var partnerGuid) ||
                template.PartnerId != partnerGuid)
                return TemplateSubmissionResult.Fail("Access denied");

            if (template.Status != PartnerTemplateStatus.Rejected)
                return TemplateSubmissionResult.Fail(
                    "Only rejected templates can be resubmitted");

            var validationErrors = ValidateTemplateForSubmission(template);
            if (validationErrors.Any())
                return TemplateSubmissionResult.ValidationFail(validationErrors);

            // DB trigger handles resubmission_count increment and submitted_at update
            template.Status = PartnerTemplateStatus.PendingReview;

            var result = await _database.UpdateAsync(template);

            if (result == null || !result.Any())
                return TemplateSubmissionResult.Fail("Failed to resubmit template");

            await MID_HelperFunctions.DebugMessageAsync(
                $"Template resubmitted: {templateId} " +
                $"(resubmission #{template.ResubmissionCount + 1})",
                LogLevel.Info);

            return TemplateSubmissionResult.Ok();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "ResubmitTemplate");
            return TemplateSubmissionResult.Fail(ex.Message);
        }
    }

    public async Task<bool> DeleteDraftAsync(string templateId, string partnerId)
    {
        try
        {
            var template = await GetRawTemplateAsync(templateId);

            if (template == null) return false;

            if (!Guid.TryParse(partnerId, out var partnerGuid) ||
                template.PartnerId != partnerGuid)
            {
                _logger.LogWarning(
                    "Partner {PartnerId} attempted to delete template {TemplateId}",
                    partnerId, templateId);
                return false;
            }

            if (template.Status != PartnerTemplateStatus.Draft)
            {
                _logger.LogWarning(
                    "Cannot delete template in status: {Status}", template.Status);
                return false;
            }

            // ── Clean up R2 images ────────────────────────────
            if (template.ImageUrls.Any())
            {
                foreach (var imageUrl in template.ImageUrls)
                {
                    var key = _r2.ExtractObjectKey(imageUrl);
                    if (!string.IsNullOrEmpty(key))
                        await _r2.DeleteFileAsync(key);
                }
            }

            await _database.DeleteAsync(template);

            await MID_HelperFunctions.DebugMessageAsync(
                $"Draft template deleted: {templateId}", LogLevel.Info);

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "DeleteDraft");
            return false;
        }
    }

    public async Task<List<PartnerTemplateViewModel>> GetPartnerTemplatesAsync(
        string partnerId,
        string? statusFilter = null)
    {
        try
        {
            var templates = await _database.GetWithFilterAsync<PartnerProductTemplateModel>(
                "partner_id", Constants.Operator.Equals, partnerId);

            var filtered = string.IsNullOrEmpty(statusFilter)
                ? templates
                : templates.Where(t => t.Status == statusFilter).ToList();

            return filtered
                .OrderByDescending(t => t.CreatedAt)
                .Select(PartnerTemplateViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetPartnerTemplates");
            return new List<PartnerTemplateViewModel>();
        }
    }

    public async Task<PartnerTemplateViewModel?> GetTemplateByIdAsync(
        string templateId,
        string? partnerId = null)
    {
        try
        {
            var template = await GetRawTemplateAsync(templateId);
            if (template == null) return null;

            // If partnerId provided enforce ownership check
            if (!string.IsNullOrEmpty(partnerId))
            {
                if (!Guid.TryParse(partnerId, out var partnerGuid) ||
                    template.PartnerId != partnerGuid)
                    return null;
            }

            return PartnerTemplateViewModel.FromCloudModel(template);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetTemplateById");
            return null;
        }
    }

    // ── Admin Actions ──────────────────────────────────────────

    public async Task<List<PartnerTemplateViewModel>> GetPendingReviewQueueAsync()
    {
        try
        {
            var templates = await _database.GetWithFilterAsync<PartnerProductTemplateModel>(
                "status",
                Constants.Operator.Equals,
                PartnerTemplateStatus.PendingReview);

            return templates
                .OrderBy(t => t.SubmittedAt)
                .Select(PartnerTemplateViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetPendingReviewQueue");
            return new List<PartnerTemplateViewModel>();
        }
    }

    public async Task<List<PartnerTemplateViewModel>> GetAllTemplatesAsync(
        string? statusFilter = null)
    {
        try
        {
            List<PartnerProductTemplateModel> templates;

            if (!string.IsNullOrEmpty(statusFilter) &&
                PartnerTemplateStatus.All.Contains(statusFilter))
            {
                templates = await _database.GetWithFilterAsync<PartnerProductTemplateModel>(
                    "status", Constants.Operator.Equals, statusFilter);
            }
            else
            {
                templates = (await _database
                    .GetAllAsync<PartnerProductTemplateModel>())
                    .ToList();
            }

            return templates
                .OrderByDescending(t => t.CreatedAt)
                .Select(PartnerTemplateViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetAllTemplates");
            return new List<PartnerTemplateViewModel>();
        }
    }

    public async Task<TemplateApprovalResult> ApproveTemplateAsync(
        ApproveTemplateRequest request)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Approving template: {request.TemplateId}", LogLevel.Info);

            var session = await _auth.GetCurrentSessionAsync();
            if (session == null)
                return new TemplateApprovalResult
                {
                    Success      = false,
                    ErrorMessage = "Admin not authenticated"
                };

            var supabaseUrl = _config["Supabase:Url"]
                ?? throw new InvalidOperationException("Supabase:Url not configured");

            var anonKey = _config["Supabase:AnonKey"] ?? string.Empty;
            var url     = $"{supabaseUrl}/functions/v1/approve-product-template";

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", session.AccessToken);
            httpRequest.Headers.Add("apikey", anonKey);

            httpRequest.Content = JsonContent.Create(new
            {
                template_id    = request.TemplateId,
                admin_user_id  = request.AdminUserId,
                price_override = request.PriceOverride,
                admin_notes    = request.AdminNotes
            });

            var response = await _http.SendAsync(httpRequest);
            var raw      = await response.Content.ReadAsStringAsync();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Approve template edge fn ({response.StatusCode}): {raw}",
                LogLevel.Debug);

            if (!response.IsSuccessStatusCode)
                return new TemplateApprovalResult
                {
                    Success      = false,
                    ErrorMessage = $"Edge function error ({response.StatusCode}): {raw}"
                };

            var result = JsonSerializer.Deserialize<EdgeTemplateApprovalResponse>(
                raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return new TemplateApprovalResult
            {
                Success           = result?.Success ?? false,
                ApprovedProductId = result?.ProductId,
                ErrorMessage      = result?.Error
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "ApproveTemplate");
            return new TemplateApprovalResult
            {
                Success      = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<bool> RejectTemplateAsync(RejectTemplateRequest request)
    {
        try
        {
            var template = await GetRawTemplateAsync(request.TemplateId);

            if (template == null)
            {
                _logger.LogWarning(
                    "Template not found for rejection: {Id}", request.TemplateId);
                return false;
            }

            if (template.Status != PartnerTemplateStatus.PendingReview)
            {
                _logger.LogWarning(
                    "Cannot reject template in status: {Status}", template.Status);
                return false;
            }

            // ── Append to rejection history ───────────────────
            var rejectionEntry = new TemplateRejectionEntry
            {
                RejectedAt    = DateTime.UtcNow,
                RejectedBy    = request.AdminUserId,
                Reason        = request.Reason,
                FieldsFlagged = request.FieldsFlagged ?? new List<string>()
            };

            template.RejectionHistory.Add(rejectionEntry);
            template.Status      = PartnerTemplateStatus.Rejected;
            template.AdminNotes  = request.AdminNotes;
            template.ReviewedBy  = request.AdminUserId;
            template.ReviewedAt  = DateTime.UtcNow;

            var result = await _database.UpdateAsync(template);

            await MID_HelperFunctions.DebugMessageAsync(
                $"Template rejected: {request.TemplateId}", LogLevel.Info);

            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "RejectTemplate");
            return false;
        }
    }

    public async Task<TemplateStatistics> GetTemplateStatisticsAsync()
    {
        try
        {
            var all = (await _database
                .GetAllAsync<PartnerProductTemplateModel>())
                .ToList();

            return new TemplateStatistics
            {
                TotalTemplates = all.Count,
                PendingReview  = all.Count(t => t.Status == PartnerTemplateStatus.PendingReview),
                Approved       = all.Count(t => t.Status == PartnerTemplateStatus.Approved),
                Rejected       = all.Count(t => t.Status == PartnerTemplateStatus.Rejected),
                Drafts         = all.Count(t => t.Status == PartnerTemplateStatus.Draft)
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetTemplateStatistics");
            return new TemplateStatistics();
        }
    }

    // ── Private Helpers ────────────────────────────────────────

    private async Task<PartnerProductTemplateModel?> GetRawTemplateAsync(
        string templateId)
    {
        try
        {
            var templates = await _database
                .GetWithFilterAsync<PartnerProductTemplateModel>(
                    "id", Constants.Operator.Equals, templateId);

            return templates.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch raw template: {Id}", templateId);
            return null;
        }
    }

    private static List<string> ValidateTemplateForSubmission(
        PartnerProductTemplateModel template)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(template.ProductName) ||
            template.ProductName.Trim().Length < 2)
            errors.Add("Product name must be at least 2 characters");

        if (string.IsNullOrWhiteSpace(template.Description) ||
            template.Description.Trim().Length < 20)
            errors.Add("Description must be at least 20 characters");

        if (string.IsNullOrWhiteSpace(template.CategoryId))
            errors.Add("Category is required");

        if (template.ProposedPrice <= 0)
            errors.Add("Price must be greater than zero");

        if (!template.Variants.Any())
            errors.Add("At least one variant is required");

        if (template.Variants.Any(v => v.Stock < 0))
            errors.Add("Variant stock cannot be negative");

        if (template.Variants.Any(v => string.IsNullOrWhiteSpace(v.Sku)))
            errors.Add("All variants must have a SKU");

        if (!template.ImageUrls.Any())
            errors.Add("At least one product image is required");

        if (template.WeightKg <= 0)
            errors.Add("Weight must be greater than zero");

        return errors;
    }

    // ── Edge Function Response Shapes ──────────────────────────

    private class EdgeTemplateApprovalResponse
    {
        public bool Success { get; set; }
        public int? ProductId { get; set; }
        public string? Error { get; set; }
    }
}
