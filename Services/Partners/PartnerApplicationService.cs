using System.Net.Http.Headers;
using System.Text.Json;
using SubashaVentures.Domain.Partner;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Services.SupaBase;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Postgrest;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Partners;

public class PartnerApplicationService : IPartnerApplicationService
{
    private readonly ISupabaseDatabaseService _database;
    private readonly ISupabaseAuthService _auth;
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<PartnerApplicationService> _logger;

    // Cooldown durations by rejection count
    private static readonly Dictionary<int, int> CooldownDaysByRejection = new()
    {
        { 1, 30  },
        { 2, 90  },
        // 3 = permanently rejected, handled separately
    };

    private const int MaxRejectionsBeforePermanent = 3;

    public PartnerApplicationService(
        ISupabaseDatabaseService database,
        ISupabaseAuthService auth,
        HttpClient http,
        IConfiguration config,
        ILogger<PartnerApplicationService> logger)
    {
        _database = database;
        _auth     = auth;
        _http     = http;
        _config   = config;
        _logger   = logger;
    }

    // ── User Actions ───────────────────────────────────────────

    public async Task<ApplicationEligibilityResult> CheckEligibilityAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
                return ApplicationEligibilityResult.NotEligible("Invalid user ID");

            if (!Guid.TryParse(userId, out var userGuid))
                return ApplicationEligibilityResult.NotEligible("Invalid user ID format");

            // Check if already a partner
            var userRecords = await _database.GetWithFilterAsync<UserModel>(
                "id", Constants.Operator.Equals, userId);

            var user = userRecords.FirstOrDefault();

            if (user == null)
                return ApplicationEligibilityResult.NotEligible("User not found");

            if (user.IsPartner)
                return new ApplicationEligibilityResult
                {
                    IsEligible        = false,
                    IsAlreadyPartner  = true,
                    Reason            = "You are already a partner"
                };

            // Load all applications for this user
            var applications = await _database.GetWithFilterAsync<PartnerApplicationModel>(
                "user_id", Constants.Operator.Equals, userId);

            // Check permanent rejection
            var permanentlyRejected = applications
                .Any(a => a.IsPermanentlyRejected);

            if (permanentlyRejected)
                return new ApplicationEligibilityResult
                {
                    IsEligible             = false,
                    IsPermanentlyRejected  = true,
                    Reason = "Your application has been permanently rejected. " +
                             "Please contact support if you believe this is an error."
                };

            // Check active application
            var activeApplication = applications
                .FirstOrDefault(a =>
                    a.Status == PartnerApplicationStatus.Pending ||
                    a.Status == PartnerApplicationStatus.UnderReview);

            if (activeApplication != null)
                return new ApplicationEligibilityResult
                {
                    IsEligible           = false,
                    HasActiveApplication = true,
                    Reason = "You already have an application under review"
                };

            // Check cooldown
            var inCooldown = applications
                .FirstOrDefault(a =>
                    a.Status == PartnerApplicationStatus.Rejected
                    && a.CooldownUntil.HasValue
                    && a.CooldownUntil.Value > DateTime.UtcNow);

            if (inCooldown != null)
                return new ApplicationEligibilityResult
                {
                    IsEligible   = false,
                    IsInCooldown = true,
                    CooldownUntil = inCooldown.CooldownUntil,
                    Reason = $"You must wait {inCooldown.CooldownRemainingDisplay} " +
                             $"before reapplying"
                };

            return ApplicationEligibilityResult.Eligible();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "CheckEligibility");
            return ApplicationEligibilityResult.NotEligible(
                "Unable to check eligibility. Please try again.");
        }
    }

    public async Task<PartnerApplicationViewModel?> SubmitApplicationAsync(
        string userId,
        SubmitPartnerApplicationRequest request)
    {
        try
        {
            // ── Eligibility gate ──────────────────────────────
            var eligibility = await CheckEligibilityAsync(userId);
            if (!eligibility.IsEligible)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Application rejected at eligibility: {eligibility.Reason}",
                    LogLevel.Warning);
                return null;
            }

            // ── Location validation (double-check server-side) ─
            if (!PartnerLocation.All.Contains(request.Location))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Invalid location: {request.Location}", LogLevel.Warning);
                return null;
            }

            // ── Field validation ──────────────────────────────
            var validationErrors = ValidateApplicationRequest(request);
            if (validationErrors.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Application validation failed: {string.Join(", ", validationErrors)}",
                    LogLevel.Warning);
                return null;
            }

            if (!Guid.TryParse(userId, out var userGuid))
                return null;

            // ── Build model ───────────────────────────────────
            var model = new PartnerApplicationModel
            {
                Id                   = Guid.NewGuid(),
                UserId               = userGuid,
                FullName             = request.FullName.Trim(),
                BusinessName         = request.BusinessName.Trim(),
                BusinessType         = request.BusinessType,
                Location             = request.Location,
                Phone                = request.Phone.Trim(),
                Email                = request.Email.Trim().ToLowerInvariant(),
                Reason               = request.Reason.Trim(),
                BankAccountName      = request.BankAccountName.Trim(),
                BankAccountNumber    = request.BankAccountNumber.Trim(),
                BankName             = request.BankName.Trim(),
                BankCode             = request.BankCode?.Trim(),
                Status               = PartnerApplicationStatus.Pending,
                RejectionCount       = 0,
                IsPermanentlyRejected = false,
                ContactLogs          = new List<ApplicationContactLog>(),
                SubmittedAt          = DateTime.UtcNow,
                CreatedAt            = DateTime.UtcNow
            };

            var inserted = await _database.InsertAsync(model);

            if (inserted == null || !inserted.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Application insert returned null", LogLevel.Error);
                return null;
            }

            // ── Update user's partner_applied_at ──────────────
            try
            {
                var userRecords = await _database.GetWithFilterAsync<UserModel>(
                    "id", Constants.Operator.Equals, userId);

                var user = userRecords.FirstOrDefault();

                if (user != null)
                {
                    user.PartnerAppliedAt = DateTime.UtcNow;
                    await _database.UpdateAsync(user);
                }
            }
            catch (Exception ex)
            {
                // Non-fatal — application was still created
                _logger.LogWarning(ex,
                    "Failed to update partner_applied_at for user {UserId}", userId);
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Partner application submitted: {inserted.First().Id}",
                LogLevel.Info);

            return PartnerApplicationViewModel.FromCloudModel(inserted.First());
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "SubmitApplication");
            return null;
        }
    }

    public async Task<PartnerApplicationViewModel?> GetUserApplicationAsync(string userId)
    {
        try
        {
            var applications = await _database.GetWithFilterAsync<PartnerApplicationModel>(
                "user_id", Constants.Operator.Equals, userId);

            var latest = applications
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefault();

            return latest == null
                ? null
                : PartnerApplicationViewModel.FromCloudModel(latest);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetUserApplication");
            return null;
        }
    }

    public async Task<List<PartnerApplicationViewModel>> GetUserApplicationHistoryAsync(
        string userId)
    {
        try
        {
            var applications = await _database.GetWithFilterAsync<PartnerApplicationModel>(
                "user_id", Constants.Operator.Equals, userId);

            return applications
                .OrderByDescending(a => a.CreatedAt)
                .Select(PartnerApplicationViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetUserApplicationHistory");
            return new List<PartnerApplicationViewModel>();
        }
    }

    // ── Admin Actions ──────────────────────────────────────────

    public async Task<List<PartnerApplicationViewModel>> GetAllApplicationsAsync(
        string? statusFilter = null)
    {
        try
        {
            List<PartnerApplicationModel> applications;

            if (!string.IsNullOrEmpty(statusFilter) &&
                PartnerApplicationStatus.All.Contains(statusFilter))
            {
                applications = await _database.GetWithFilterAsync<PartnerApplicationModel>(
                    "status", Constants.Operator.Equals, statusFilter);
            }
            else
            {
                applications = (await _database.GetAllAsync<PartnerApplicationModel>())
                    .ToList();
            }

            return applications
                .OrderByDescending(a => a.SubmittedAt)
                .Select(PartnerApplicationViewModel.FromCloudModel)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetAllApplications");
            return new List<PartnerApplicationViewModel>();
        }
    }

    public async Task<PartnerApplicationViewModel?> GetApplicationByIdAsync(
        string applicationId)
    {
        try
        {
            if (!Guid.TryParse(applicationId, out _)) return null;

            var applications = await _database.GetWithFilterAsync<PartnerApplicationModel>(
                "id", Constants.Operator.Equals, applicationId);

            var application = applications.FirstOrDefault();

            return application == null
                ? null
                : PartnerApplicationViewModel.FromCloudModel(application);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetApplicationById");
            return null;
        }
    }

    public async Task<bool> MarkUnderReviewAsync(
        string applicationId,
        string adminUserId)
    {
        try
        {
            var applications = await _database.GetWithFilterAsync<PartnerApplicationModel>(
                "id", Constants.Operator.Equals, applicationId);

            var application = applications.FirstOrDefault();

            if (application == null)
            {
                _logger.LogWarning("Application not found: {Id}", applicationId);
                return false;
            }

            if (application.Status != PartnerApplicationStatus.Pending)
            {
                _logger.LogWarning(
                    "Cannot mark as under review — current status: {Status}",
                    application.Status);
                return false;
            }

            application.Status     = PartnerApplicationStatus.UnderReview;
            application.ReviewedBy = adminUserId;
            application.ReviewedAt = DateTime.UtcNow;

            var result = await _database.UpdateAsync(application);
            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "MarkUnderReview");
            return false;
        }
    }

    public async Task<bool> AddContactLogAsync(AddContactLogRequest request)
    {
        try
        {
            var applications = await _database.GetWithFilterAsync<PartnerApplicationModel>(
                "id", Constants.Operator.Equals, request.ApplicationId);

            var application = applications.FirstOrDefault();

            if (application == null) return false;

            var logEntry = new ApplicationContactLog
            {
                LogId         = Guid.NewGuid().ToString(),
                LoggedBy      = request.AdminUserId,
                LoggedByName  = request.AdminName,
                LoggedAt      = DateTime.UtcNow,
                ContactMethod = request.ContactMethod,
                ContactDate   = request.ContactDate,
                Notes         = request.Notes,
                Outcome       = request.Outcome
            };

            application.ContactLogs.Add(logEntry);

            var result = await _database.UpdateAsync(application);

            await MID_HelperFunctions.DebugMessageAsync(
                $"Contact log added to application {request.ApplicationId}",
                LogLevel.Info);

            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "AddContactLog");
            return false;
        }
    }

    public async Task<ApplicationApprovalResult> ApproveApplicationAsync(
        string applicationId,
        string adminUserId)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Approving application: {applicationId}", LogLevel.Info);

            var session = await _auth.GetCurrentSessionAsync();
            if (session == null)
                return new ApplicationApprovalResult
                {
                    Success      = false,
                    ErrorMessage = "Admin not authenticated"
                };

            // ── Call edge function ────────────────────────────
            var supabaseUrl = _config["Supabase:Url"]
                ?? throw new InvalidOperationException("Supabase:Url not configured");

            var url     = $"{supabaseUrl}/functions/v1/approve-partner-application";
            var anonKey = _config["Supabase:AnonKey"] ?? string.Empty;

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", session.AccessToken);
            httpRequest.Headers.Add("apikey", anonKey);

            httpRequest.Content = JsonContent.Create(new
            {
                application_id = applicationId,
                admin_user_id  = adminUserId
            });

            var response = await _http.SendAsync(httpRequest);
            var raw      = await response.Content.ReadAsStringAsync();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Edge fn response ({response.StatusCode}): {raw}",
                LogLevel.Debug);

            if (!response.IsSuccessStatusCode)
            {
                return new ApplicationApprovalResult
                {
                    Success      = false,
                    ErrorMessage = $"Edge function error ({response.StatusCode}): {raw}"
                };
            }

            var result = JsonSerializer.Deserialize<EdgeApprovalResponse>(
                raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return new ApplicationApprovalResult
            {
                Success        = result?.Success ?? false,
                PartnerId      = result?.PartnerId,
                PartnerStoreId = result?.StoreId,
                ErrorMessage   = result?.Error
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "ApproveApplication");
            return new ApplicationApprovalResult
            {
                Success      = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<bool> RejectApplicationAsync(
        string applicationId,
        string adminUserId,
        string rejectionReason,
        int? cooldownDaysOverride = null)
    {
        try
        {
            var applications = await _database.GetWithFilterAsync<PartnerApplicationModel>(
                "id", Constants.Operator.Equals, applicationId);

            var application = applications.FirstOrDefault();

            if (application == null)
            {
                _logger.LogWarning("Application not found: {Id}", applicationId);
                return false;
            }

            var newRejectionCount = application.RejectionCount + 1;

            // ── Permanent rejection at 3 strikes ──────────────
            if (newRejectionCount >= MaxRejectionsBeforePermanent)
            {
                application.Status                = PartnerApplicationStatus.PermanentlyRejected;
                application.IsPermanentlyRejected = true;
                application.CooldownUntil         = null;

                await MID_HelperFunctions.DebugMessageAsync(
                    $"Application {applicationId} permanently rejected " +
                    $"(strike {newRejectionCount})",
                    LogLevel.Warning);
            }
            else
            {
                // ── Cooldown calculation ───────────────────────
                var cooldownDays = cooldownDaysOverride
                    ?? CooldownDaysByRejection.GetValueOrDefault(newRejectionCount, 30);

                application.Status        = PartnerApplicationStatus.Rejected;
                application.CooldownUntil = DateTime.UtcNow.AddDays(cooldownDays);

                await MID_HelperFunctions.DebugMessageAsync(
                    $"Application {applicationId} rejected " +
                    $"(strike {newRejectionCount}, cooldown {cooldownDays} days)",
                    LogLevel.Info);
            }

            application.RejectionCount  = (short)newRejectionCount;
            application.RejectionReason = rejectionReason;
            application.ReviewedBy      = adminUserId;
            application.ReviewedAt      = DateTime.UtcNow;

            var result = await _database.UpdateAsync(application);
            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "RejectApplication");
            return false;
        }
    }

    public async Task<ApplicationStatistics> GetApplicationStatisticsAsync()
    {
        try
        {
            var all = (await _database.GetAllAsync<PartnerApplicationModel>()).ToList();

            return new ApplicationStatistics
            {
                Total               = all.Count,
                Pending             = all.Count(a => a.Status == PartnerApplicationStatus.Pending),
                UnderReview         = all.Count(a => a.Status == PartnerApplicationStatus.UnderReview),
                Approved            = all.Count(a => a.Status == PartnerApplicationStatus.Approved),
                Rejected            = all.Count(a => a.Status == PartnerApplicationStatus.Rejected),
                PermanentlyRejected = all.Count(a => a.IsPermanentlyRejected),
                AbujaApplications   = all.Count(a => a.Location == PartnerLocation.Abuja),
                KadunaApplications  = all.Count(a => a.Location == PartnerLocation.Kaduna)
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "GetApplicationStatistics");
            return new ApplicationStatistics();
        }
    }

    // ── Private Helpers ────────────────────────────────────────

    private static List<string> ValidateApplicationRequest(
        SubmitPartnerApplicationRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.FullName) ||
            request.FullName.Trim().Length < 2)
            errors.Add("Full name must be at least 2 characters");

        if (string.IsNullOrWhiteSpace(request.BusinessName) ||
            request.BusinessName.Trim().Length < 2)
            errors.Add("Business name must be at least 2 characters");

        if (!PartnerBusinessType.All.Contains(request.BusinessType))
            errors.Add("Invalid business type");

        if (!PartnerLocation.All.Contains(request.Location))
            errors.Add($"Location must be one of: {string.Join(", ", PartnerLocation.All)}");

        if (string.IsNullOrWhiteSpace(request.Phone) ||
            request.Phone.Trim().Length < 10)
            errors.Add("Phone number must be at least 10 digits");

        if (string.IsNullOrWhiteSpace(request.Email) ||
            !request.Email.Contains('@'))
            errors.Add("Invalid email address");

        if (string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Trim().Length < 50)
            errors.Add("Reason must be at least 50 characters");

        if (string.IsNullOrWhiteSpace(request.BankAccountName) ||
            request.BankAccountName.Trim().Length < 2)
            errors.Add("Bank account name is required");

        if (string.IsNullOrWhiteSpace(request.BankAccountNumber) ||
            !request.BankAccountNumber.All(char.IsDigit) ||
            request.BankAccountNumber.Length is < 10 or > 12)
            errors.Add("Bank account number must be 10-12 digits");

        if (string.IsNullOrWhiteSpace(request.BankName))
            errors.Add("Bank name is required");

        return errors;
    }

    // ── Edge Function Response Shape ───────────────────────────

    private class EdgeApprovalResponse
    {
        public bool Success { get; set; }
        public string? PartnerId { get; set; }
        public string? StoreId { get; set; }
        public string? Error { get; set; }
    }
}
