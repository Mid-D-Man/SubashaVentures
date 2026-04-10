using SubashaVentures.Domain.Partner;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Services.SupaBase;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Postgrest;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Partners;

public class PartnerStoreService : IPartnerStoreService
{
    private readonly ISupabaseDatabaseService _database;
    private readonly ILogger<PartnerStoreService> _logger;

    public PartnerStoreService(
        ISupabaseDatabaseService database,
        ILogger<PartnerStoreService> logger)
    {
        _database = database;
        _logger   = logger;
    }

    // ── Store Profile ──────────────────────────────────────────

    public async Task<PartnerStoreViewModel?> GetStoreByPartnerIdAsync(string partnerId)
    {
        try
        {
            var stores = await _database.GetWithFilterAsync<PartnerStoreModel>(
                "partner_id", Constants.Operator.Equals, partnerId);

            var store = stores.FirstOrDefault();
            return store == null ? null : PartnerStoreViewModel.FromCloudModel(store);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetStoreByPartnerId");
            return null;
        }
    }

    public async Task<PartnerStoreViewModel?> GetStoreBySlugAsync(string slug)
    {
        try
        {
            var stores = await _database.GetWithFilterAsync<PartnerStoreModel>(
                "store_slug", Constants.Operator.Equals, slug.ToLowerInvariant());

            var store = stores.FirstOrDefault(s => s.IsActive);
            return store == null ? null : PartnerStoreViewModel.FromCloudModel(store);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetStoreBySlug");
            return null;
        }
    }

    public async Task<List<PartnerStoreViewModel>> GetAllActiveStoresAsync()
    {
        try
        {
            var stores = await _database.GetWithFilterAsync<PartnerStoreModel>(
                "is_active", Constants.Operator.Equals, true);

            return stores
                .OrderBy(s => s.StoreName)
                .Select(PartnerStoreViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetAllActiveStores");
            return new List<PartnerStoreViewModel>();
        }
    }

    public async Task<bool> UpdateStoreAsync(
        string storeId,
        string partnerId,
        UpdateStoreRequest request)
    {
        try
        {
            var stores = await _database.GetWithFilterAsync<PartnerStoreModel>(
                "id", Constants.Operator.Equals, storeId);

            var store = stores.FirstOrDefault();

            if (store == null)
            {
                _logger.LogWarning("Store not found: {StoreId}", storeId);
                return false;
            }

            if (!Guid.TryParse(partnerId, out var partnerGuid) ||
                store.PartnerId != partnerGuid)
            {
                _logger.LogWarning(
                    "Partner {PartnerId} attempted to update store {StoreId}",
                    partnerId, storeId);
                return false;
            }

            // ── Apply only display fields ──────────────────────
            // is_active, store_slug, partner_id are intentionally
            // NOT updateable here — admin-only via direct DB access
            if (request.StoreName  != null) store.StoreName  = request.StoreName.Trim();
            if (request.Tagline    != null) store.Tagline    = request.Tagline.Trim();
            if (request.Description != null) store.Description = request.Description.Trim();
            if (request.LogoUrl    != null) store.LogoUrl    = request.LogoUrl;
            if (request.BannerUrl  != null) store.BannerUrl  = request.BannerUrl;
            if (request.PublicPhone != null)
                store.PublicPhone = string.IsNullOrWhiteSpace(request.PublicPhone)
                    ? null : request.PublicPhone.Trim();
            if (request.PublicEmail != null)
                store.PublicEmail = string.IsNullOrWhiteSpace(request.PublicEmail)
                    ? null : request.PublicEmail.Trim().ToLowerInvariant();

            var result = await _database.UpdateAsync(store);

            await MID_HelperFunctions.DebugMessageAsync(
                $"Store updated: {storeId}", LogLevel.Info);

            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "UpdateStore");
            return false;
        }
    }

    // ── Partner Dashboard ──────────────────────────────────────

    public async Task<PartnerDashboardViewModel?> GetDashboardAsync(
        string partnerId,
        string userId)
    {
        try
        {
            // ── Fetch partner record ──────────────────────────
            var partners = await _database.GetWithFilterAsync<PartnerModel>(
                "id", Constants.Operator.Equals, partnerId);

            var partner = partners.FirstOrDefault();
            if (partner == null) return null;

            // ── Fetch in parallel ──────────────────────────────
            var storeTask       = GetStoreByPartnerIdAsync(partnerId);
            var templatesTask   = _database.GetWithFilterAsync<PartnerProductTemplateModel>(
                "partner_id", Constants.Operator.Equals, partnerId);
            var payoutsTask     = _database.GetWithFilterAsync<PartnerPayoutRequestModel>(
                "partner_id", Constants.Operator.Equals, partnerId);

            await Task.WhenAll(storeTask, templatesTask, payoutsTask);

            var store     = await storeTask;
            var templates = await templatesTask;
            var payouts   = await payoutsTask;

            // ── Template breakdown ────────────────────────────
            var draftCount    = templates.Count(t => t.Status == PartnerTemplateStatus.Draft);
            var pendingCount  = templates.Count(t => t.Status == PartnerTemplateStatus.PendingReview);
            var rejectedCount = templates.Count(t => t.Status == PartnerTemplateStatus.Rejected);
            var approvedCount = templates.Count(t => t.Status == PartnerTemplateStatus.Approved);

            // ── Payout status ──────────────────────────────────
            var hasOpenPayout = payouts.Any(p =>
                PayoutRequestStatus.Open.Contains(p.Status));

            var recentPayouts = payouts
                .OrderByDescending(p => p.RequestedAt)
                .Take(5)
                .Select(PartnerPayoutRequestViewModel.FromCloudModel)
                .ToList();

            // ── Build alerts ───────────────────────────────────
            var alerts = BuildDashboardAlerts(
                partner, templates.ToList(), payouts.ToList(), store);

            var dashboard = new PartnerDashboardViewModel
            {
                PartnerId              = partner.Id.ToString(),
                UserId                 = userId,
                PartnerName            = partner.Name,
                UniquePartnerId        = partner.UniquePartnerId,
                CommissionRate         = partner.CommissionRate.ToString("F0"),
                VerificationStatus     = partner.VerificationStatus,
                Store                  = store,
                TotalApprovedProducts  = approvedCount,
                TotalDraftTemplates    = draftCount,
                TotalPendingTemplates  = pendingCount,
                TotalRejectedTemplates = rejectedCount,
                PendingPayout          = partner.PendingPayout,
                TotalRevenue           = partner.TotalSales,
                TotalWithdrawn         = partner.TotalSales - partner.PendingPayout,
                HasOpenPayoutRequest   = hasOpenPayout,
                RecentTemplates        = templates
                    .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
                    .Take(5)
                    .Select(PartnerTemplateViewModel.FromCloudModel)
                    .ToList(),
                RecentPayouts          = recentPayouts,
                Alerts                 = alerts
            };

            return dashboard;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetDashboard");
            return null;
        }
    }

    // ── Financials ─────────────────────────────────────────────

    public async Task<PartnerEarningsSummary?> GetEarningsSummaryAsync(string partnerId)
    {
        try
        {
            var partners = await _database.GetWithFilterAsync<PartnerModel>(
                "id", Constants.Operator.Equals, partnerId);

            var partner = partners.FirstOrDefault();
            if (partner == null) return null;

            var payouts     = await _database.GetWithFilterAsync<PartnerPayoutRequestModel>(
                "partner_id", Constants.Operator.Equals, partnerId);

            var bankRequests = await _database
                .GetWithFilterAsync<PartnerBankUpdateRequestModel>(
                    "partner_id", Constants.Operator.Equals, partnerId);

            var totalWithdrawn = payouts
                .Where(p => p.Status == PayoutRequestStatus.Paid)
                .Sum(p => p.AmountRequested);

            var hasOpenPayout = payouts
                .Any(p => PayoutRequestStatus.Open.Contains(p.Status));

            var hasPendingBankUpdate = bankRequests
                .Any(b => b.Status == BankUpdateRequestStatus.Pending);

            return new PartnerEarningsSummary
            {
                PartnerId                  = partnerId,
                TotalRevenue               = partner.TotalSales,
                TotalCommissionPaid        = partner.TotalCommission,
                PendingPayout              = partner.PendingPayout,
                TotalWithdrawn             = totalWithdrawn,
                HasOpenPayoutRequest       = hasOpenPayout,
                RecentPayoutRequests       = payouts
                    .OrderByDescending(p => p.RequestedAt)
                    .Take(10)
                    .Select(PartnerPayoutRequestViewModel.FromCloudModel)
                    .ToList(),
                CurrentBankAccountName     = partner.BankDetails?.AccountName,
                CurrentBankAccountNumber   = partner.BankDetails?.AccountNumber,
                CurrentBankName            = partner.BankDetails?.BankName,
                HasPendingBankUpdateRequest = hasPendingBankUpdate
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetEarningsSummary");
            return null;
        }
    }

    public async Task<PayoutSubmissionResult> RequestPayoutAsync(
        string partnerId,
        string userId,
        RequestPayoutRequest request)
    {
        try
        {
            if (!Guid.TryParse(partnerId, out var partnerGuid) ||
                !Guid.TryParse(userId, out var userGuid))
                return PayoutSubmissionResult.Fail("Invalid IDs");

            // ── Fetch partner ─────────────────────────────────
            var partners = await _database.GetWithFilterAsync<PartnerModel>(
                "id", Constants.Operator.Equals, partnerId);

            var partner = partners.FirstOrDefault();
            if (partner == null)
                return PayoutSubmissionResult.Fail("Partner not found");

            // ── Validate threshold ────────────────────────────
            if (partner.PendingPayout < PayoutConstants.MinimumPayoutAmount)
                return PayoutSubmissionResult.Fail(
                    $"Minimum payout is {PayoutConstants.MinimumPayoutAmount:N0}. " +
                    $"Your balance is ₦{partner.PendingPayout:N0}");

            if (request.AmountRequested > partner.PendingPayout)
                return PayoutSubmissionResult.Fail(
                    $"Requested amount exceeds available balance " +
                    $"of ₦{partner.PendingPayout:N0}");

            if (request.AmountRequested < PayoutConstants.MinimumPayoutAmount)
                return PayoutSubmissionResult.Fail(
                    $"Minimum payout is ₦{PayoutConstants.MinimumPayoutAmount:N0}");

            // ── Check for open request ────────────────────────
            var existingPayouts = await _database
                .GetWithFilterAsync<PartnerPayoutRequestModel>(
                    "partner_id", Constants.Operator.Equals, partnerId);

            var hasOpenRequest = existingPayouts
                .Any(p => PayoutRequestStatus.Open.Contains(p.Status));

            if (hasOpenRequest)
                return PayoutSubmissionResult.Fail(
                    "You already have an open payout request");

            // ── Validate bank details ─────────────────────────
            if (partner.BankDetails == null ||
                string.IsNullOrEmpty(partner.BankDetails.AccountNumber))
                return PayoutSubmissionResult.Fail(
                    "No bank details found. Please add bank details first");

            // ── Create request ────────────────────────────────
            var payoutRequest = new PartnerPayoutRequestModel
            {
                Id                 = Guid.NewGuid(),
                PartnerId          = partnerGuid,
                UserId             = userGuid,
                AmountRequested    = request.AmountRequested,
                BankAccountName    = partner.BankDetails.AccountName,
                BankAccountNumber  = partner.BankDetails.AccountNumber,
                BankName           = partner.BankDetails.BankName,
                BankCode           = partner.BankDetails.BankCode,
                Status             = PayoutRequestStatus.Pending,
                PaymentMethod      = PayoutConstants.DefaultPaymentMethod,
                RequestedAt        = DateTime.UtcNow,
                CreatedAt          = DateTime.UtcNow
            };

            var result = await _database.InsertAsync(payoutRequest);

            if (result == null || !result.Any())
                return PayoutSubmissionResult.Fail("Failed to create payout request");

            await MID_HelperFunctions.DebugMessageAsync(
                $"Payout request created: {result.First().Id} " +
                $"(₦{request.AmountRequested:N0})",
                LogLevel.Info);

            return PayoutSubmissionResult.Ok(result.First().Id.ToString());
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "RequestPayout");
            return PayoutSubmissionResult.Fail(ex.Message);
        }
    }

    public async Task<bool> CancelPayoutRequestAsync(
        string payoutRequestId,
        string partnerId)
    {
        try
        {
            var requests = await _database.GetWithFilterAsync<PartnerPayoutRequestModel>(
                "id", Constants.Operator.Equals, payoutRequestId);

            var request = requests.FirstOrDefault();
            if (request == null) return false;

            if (!Guid.TryParse(partnerId, out var partnerGuid) ||
                request.PartnerId != partnerGuid)
                return false;

            if (request.Status != PayoutRequestStatus.Pending)
            {
                _logger.LogWarning(
                    "Cannot cancel payout in status: {Status}", request.Status);
                return false;
            }

            await _database.DeleteAsync(request);

            await MID_HelperFunctions.DebugMessageAsync(
                $"Payout request cancelled: {payoutRequestId}", LogLevel.Info);

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "CancelPayoutRequest");
            return false;
        }
    }

    public async Task<bool> RequestBankUpdateAsync(
        string partnerId,
        string userId,
        RequestBankUpdateRequest request)
    {
        try
        {
            if (!Guid.TryParse(partnerId, out var partnerGuid) ||
                !Guid.TryParse(userId, out var userGuid))
                return false;

            // ── Validate no pending request ───────────────────
            var existing = await _database
                .GetWithFilterAsync<PartnerBankUpdateRequestModel>(
                    "partner_id", Constants.Operator.Equals, partnerId);

            if (existing.Any(r => r.Status == BankUpdateRequestStatus.Pending))
            {
                _logger.LogWarning(
                    "Partner {PartnerId} already has a pending bank update",
                    partnerId);
                return false;
            }

            // ── Snapshot current bank details ─────────────────
            var partners = await _database.GetWithFilterAsync<PartnerModel>(
                "id", Constants.Operator.Equals, partnerId);

            var partner = partners.FirstOrDefault();

            var model = new PartnerBankUpdateRequestModel
            {
                Id               = Guid.NewGuid(),
                PartnerId        = partnerGuid,
                UserId           = userGuid,
                NewAccountName   = request.NewAccountName.Trim(),
                NewAccountNumber = request.NewAccountNumber.Trim(),
                NewBankName      = request.NewBankName.Trim(),
                NewBankCode      = request.NewBankCode?.Trim(),
                OldAccountName   = partner?.BankDetails?.AccountName,
                OldAccountNumber = partner?.BankDetails?.AccountNumber,
                OldBankName      = partner?.BankDetails?.BankName,
                Reason           = request.Reason.Trim(),
                Status           = BankUpdateRequestStatus.Pending,
                RequestedAt      = DateTime.UtcNow,
                CreatedAt        = DateTime.UtcNow
            };

            var result = await _database.InsertAsync(model);

            await MID_HelperFunctions.DebugMessageAsync(
                $"Bank update request created for partner {partnerId}",
                LogLevel.Info);

            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "RequestBankUpdate");
            return false;
        }
    }

    public async Task<List<PartnerPayoutRequestViewModel>> GetPayoutRequestsAsync(
        string partnerId)
    {
        try
        {
            var requests = await _database.GetWithFilterAsync<PartnerPayoutRequestModel>(
                "partner_id", Constants.Operator.Equals, partnerId);

            return requests
                .OrderByDescending(r => r.RequestedAt)
                .Select(PartnerPayoutRequestViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetPayoutRequests");
            return new List<PartnerPayoutRequestViewModel>();
        }
    }

    public async Task<List<PartnerBankUpdateRequestViewModel>> GetBankUpdateRequestsAsync(
        string partnerId)
    {
        try
        {
            var requests = await _database
                .GetWithFilterAsync<PartnerBankUpdateRequestModel>(
                    "partner_id", Constants.Operator.Equals, partnerId);

            return requests
                .OrderByDescending(r => r.RequestedAt)
                .Select(PartnerBankUpdateRequestViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetBankUpdateRequests");
            return new List<PartnerBankUpdateRequestViewModel>();
        }
    }

    // ── Admin: Payout Management ───────────────────────────────

    public async Task<List<PartnerPayoutRequestViewModel>> GetAllPayoutRequestsAsync(
        string? statusFilter = null)
    {
        try
        {
            List<PartnerPayoutRequestModel> requests;

            if (!string.IsNullOrEmpty(statusFilter) &&
                PayoutRequestStatus.All.Contains(statusFilter))
            {
                requests = await _database.GetWithFilterAsync<PartnerPayoutRequestModel>(
                    "status", Constants.Operator.Equals, statusFilter);
            }
            else
            {
                requests = (await _database
                    .GetAllAsync<PartnerPayoutRequestModel>())
                    .ToList();
            }

            return requests
                .OrderByDescending(r => r.RequestedAt)
                .Select(PartnerPayoutRequestViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetAllPayoutRequests");
            return new List<PartnerPayoutRequestViewModel>();
        }
    }

    public async Task<bool> MarkPayoutProcessingAsync(
        string payoutRequestId,
        string adminUserId)
    {
        try
        {
            var requests = await _database.GetWithFilterAsync<PartnerPayoutRequestModel>(
                "id", Constants.Operator.Equals, payoutRequestId);

            var request = requests.FirstOrDefault();
            if (request == null || request.Status != PayoutRequestStatus.Pending)
                return false;

            request.Status      = PayoutRequestStatus.Processing;
            request.ProcessedBy = adminUserId;
            request.ProcessedAt = DateTime.UtcNow;

            var result = await _database.UpdateAsync(request);
            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "MarkPayoutProcessing");
            return false;
        }
    }

    public async Task<bool> MarkPayoutPaidAsync(MarkPayoutPaidRequest request)
    {
        try
        {
            var requests = await _database.GetWithFilterAsync<PartnerPayoutRequestModel>(
                "id", Constants.Operator.Equals, request.PayoutRequestId);

            var payoutRequest = requests.FirstOrDefault();

            if (payoutRequest == null ||
                payoutRequest.Status == PayoutRequestStatus.Paid)
                return false;

            payoutRequest.Status               = PayoutRequestStatus.Paid;
            payoutRequest.TransactionReference = request.TransactionReference;
            payoutRequest.PaymentMethod        = request.PaymentMethod;
            payoutRequest.AdminNotes           = request.AdminNotes;
            payoutRequest.ProcessedBy          = request.AdminUserId;
            payoutRequest.ProcessedAt          = DateTime.UtcNow;

            var updateResult = await _database.UpdateAsync(payoutRequest);

            if (updateResult == null || !updateResult.Any()) return false;

            // ── Deduct from partner pending_payout ────────────
            var partners = await _database.GetWithFilterAsync<PartnerModel>(
                "id", Constants.Operator.Equals, payoutRequest.PartnerId.ToString());

            var partner = partners.FirstOrDefault();
            if (partner != null)
            {
                partner.PendingPayout = Math.Max(
                    0,
                    partner.PendingPayout - payoutRequest.AmountRequested);

                await _database.UpdateAsync(partner);
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Payout marked paid: {request.PayoutRequestId} " +
                $"(₦{payoutRequest.AmountRequested:N0}, ref: {request.TransactionReference})",
                LogLevel.Info);

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "MarkPayoutPaid");
            return false;
        }
    }

    public async Task<bool> RejectPayoutRequestAsync(
        string payoutRequestId,
        string adminUserId,
        string reason)
    {
        try
        {
            var requests = await _database.GetWithFilterAsync<PartnerPayoutRequestModel>(
                "id", Constants.Operator.Equals, payoutRequestId);

            var request = requests.FirstOrDefault();
            if (request == null || request.IsTerminal) return false;

            request.Status          = PayoutRequestStatus.Rejected;
            request.RejectionReason = reason;
            request.ProcessedBy     = adminUserId;
            request.ProcessedAt     = DateTime.UtcNow;

            var result = await _database.UpdateAsync(request);
            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "RejectPayoutRequest");
            return false;
        }
    }

    // ── Admin: Bank Update Management ─────────────────────────

    public async Task<List<PartnerBankUpdateRequestViewModel>> GetAllBankUpdateRequestsAsync(
        string? statusFilter = null)
    {
        try
        {
            List<PartnerBankUpdateRequestModel> requests;

            if (!string.IsNullOrEmpty(statusFilter))
            {
                requests = await _database
                    .GetWithFilterAsync<PartnerBankUpdateRequestModel>(
                        "status", Constants.Operator.Equals, statusFilter);
            }
            else
            {
                requests = (await _database
                    .GetAllAsync<PartnerBankUpdateRequestModel>())
                    .ToList();
            }

            return requests
                .OrderByDescending(r => r.RequestedAt)
                .Select(PartnerBankUpdateRequestViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetAllBankUpdateRequests");
            return new List<PartnerBankUpdateRequestViewModel>();
        }
    }

    public async Task<bool> ApproveBankUpdateAsync(
        string requestId,
        string adminUserId,
        string? adminNotes = null)
    {
        try
        {
            var requests = await _database
                .GetWithFilterAsync<PartnerBankUpdateRequestModel>(
                    "id", Constants.Operator.Equals, requestId);

            var bankRequest = requests.FirstOrDefault();

            if (bankRequest == null ||
                bankRequest.Status != BankUpdateRequestStatus.Pending)
                return false;

            // ── Update partner bank details ───────────────────
            var partners = await _database.GetWithFilterAsync<PartnerModel>(
                "id", Constants.Operator.Equals, bankRequest.PartnerId.ToString());

            var partner = partners.FirstOrDefault();

            if (partner != null)
            {
                partner.BankDetails = new BankDetails
                {
                    AccountName   = bankRequest.NewAccountName,
                    AccountNumber = bankRequest.NewAccountNumber,
                    BankName      = bankRequest.NewBankName,
                    BankCode      = bankRequest.NewBankCode
                };

                partner.UpdatedAt = DateTime.UtcNow;
                await _database.UpdateAsync(partner);
            }

            // ── Mark request as approved ──────────────────────
            bankRequest.Status      = BankUpdateRequestStatus.Approved;
            bankRequest.ReviewedBy  = adminUserId;
            bankRequest.ReviewedAt  = DateTime.UtcNow;
            bankRequest.AdminNotes  = adminNotes;

            var result = await _database.UpdateAsync(bankRequest);

            await MID_HelperFunctions.DebugMessageAsync(
                $"Bank update approved for partner {bankRequest.PartnerId}",
                LogLevel.Info);

            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "ApproveBankUpdate");
            return false;
        }
    }

    public async Task<bool> RejectBankUpdateAsync(
        string requestId,
        string adminUserId,
        string rejectionReason)
    {
        try
        {
            var requests = await _database
                .GetWithFilterAsync<PartnerBankUpdateRequestModel>(
                    "id", Constants.Operator.Equals, requestId);

            var bankRequest = requests.FirstOrDefault();

            if (bankRequest == null ||
                bankRequest.Status != BankUpdateRequestStatus.Pending)
                return false;

            bankRequest.Status          = BankUpdateRequestStatus.Rejected;
            bankRequest.RejectionReason = rejectionReason;
            bankRequest.ReviewedBy      = adminUserId;
            bankRequest.ReviewedAt      = DateTime.UtcNow;

            var result = await _database.UpdateAsync(bankRequest);
            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "RejectBankUpdate");
            return false;
        }
    }

    // ── Private: Alert Builder ─────────────────────────────────

    private static List<PartnerAlert> BuildDashboardAlerts(
        PartnerModel partner,
        List<PartnerProductTemplateModel> templates,
        List<PartnerPayoutRequestModel> payouts,
        PartnerStoreViewModel? store)
    {
        var alerts = new List<PartnerAlert>();

        if (partner.VerificationStatus == "pending")
            alerts.Add(PartnerAlert.Warning(
                "Your account is pending verification. " +
                "Our team will contact you shortly."));

        if (store == null)
            alerts.Add(PartnerAlert.Warning(
                "Your store profile is not yet set up. " +
                "Complete it to appear in our store directory.",
                actionUrl: "user/partner/store",
                actionLabel: "Set Up Store"));

        var rejectedTemplates = templates
            .Count(t => t.Status == PartnerTemplateStatus.Rejected);

        if (rejectedTemplates > 0)
            alerts.Add(PartnerAlert.Danger(
                $"You have {rejectedTemplates} rejected product template(s). " +
                $"Review the feedback and resubmit.",
                actionUrl: "user/partner/templates",
                actionLabel: "View Templates"));

        var processingPayout = payouts
            .Any(p => p.Status == PayoutRequestStatus.Processing);

        if (processingPayout)
            alerts.Add(PartnerAlert.Info(
                "Your payout request is being processed. " +
                "You will be notified when the transfer is complete."));

        if (partner.PendingPayout >= PayoutConstants.MinimumPayoutAmount &&
            !payouts.Any(p => PayoutRequestStatus.Open.Contains(p.Status)))
            alerts.Add(PartnerAlert.Success(
                $"Your balance of ₦{partner.PendingPayout:N0} is ready for withdrawal.",
                actionUrl: "user/partner/financials",
                actionLabel: "Request Payout"));

        return alerts;
    }
}
