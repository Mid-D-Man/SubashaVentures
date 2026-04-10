using SubashaVentures.Models.Supabase;

namespace SubashaVentures.Domain.Partner;

/// <summary>
/// View model for partner applications.
/// Used in both the user-facing apply page and the admin review panel.
/// </summary>
public class PartnerApplicationViewModel
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;

    // ── Applicant & Business Info ──────────────────────────────
    public string FullName { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string BusinessType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;

    // ── Bank Details ───────────────────────────────────────────
    public string BankAccountName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string? BankCode { get; set; }

    // ── Status ─────────────────────────────────────────────────
    public string Status { get; set; } = PartnerApplicationStatus.Pending;

    // ── Rejection & Cooldown ───────────────────────────────────
    public string? RejectionReason { get; set; }
    public int RejectionCount { get; set; }
    public DateTime? CooldownUntil { get; set; }
    public bool IsPermanentlyRejected { get; set; }

    // ── Contact Logs ──────────────────────────────────────────
    public List<ApplicationContactLogViewModel> ContactLogs { get; set; } = new();

    // ── Admin Audit ────────────────────────────────────────────
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }

    // ── Timestamps ────────────────────────────────────────────
    public DateTime SubmittedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // ── Computed Display ───────────────────────────────────────
    public bool IsInCooldown =>
        CooldownUntil.HasValue && CooldownUntil.Value > DateTime.UtcNow;

    public bool CanReapply =>
        !IsPermanentlyRejected
        && !IsInCooldown
        && Status == PartnerApplicationStatus.Rejected;

    public string MaskedAccountNumber =>
        BankAccountNumber.Length >= 4
            ? string.Concat(
                new string('*', BankAccountNumber.Length - 4),
                BankAccountNumber[^4..])
            : BankAccountNumber;

    public string StatusDisplay => Status switch
    {
        PartnerApplicationStatus.Pending             => "Pending Review",
        PartnerApplicationStatus.UnderReview         => "Under Review",
        PartnerApplicationStatus.Approved            => "Approved",
        PartnerApplicationStatus.Rejected            => "Rejected",
        PartnerApplicationStatus.PermanentlyRejected => "Permanently Rejected",
        _                                            => Status
    };

    public string StatusBadgeClass => Status switch
    {
        PartnerApplicationStatus.Pending             => "badge-warning",
        PartnerApplicationStatus.UnderReview         => "badge-info",
        PartnerApplicationStatus.Approved            => "badge-success",
        PartnerApplicationStatus.Rejected            => "badge-danger",
        PartnerApplicationStatus.PermanentlyRejected => "badge-dark",
        _                                            => "badge-secondary"
    };

    public string CooldownRemainingDisplay
    {
        get
        {
            if (!IsInCooldown) return string.Empty;
            var ts = CooldownUntil!.Value - DateTime.UtcNow;
            if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays} day(s)";
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours} hour(s)";
            return $"{(int)ts.TotalMinutes} minute(s)";
        }
    }

    public string SubmittedAtDisplay =>
        SubmittedAt.ToString("MMM dd, yyyy HH:mm");

    public string ReviewedAtDisplay =>
        ReviewedAt?.ToString("MMM dd, yyyy HH:mm") ?? "Not yet reviewed";

    // ── Conversion ─────────────────────────────────────────────

    public static PartnerApplicationViewModel FromCloudModel(PartnerApplicationModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        return new PartnerApplicationViewModel
        {
            Id                   = model.Id.ToString(),
            UserId               = model.UserId.ToString(),
            FullName             = model.FullName,
            BusinessName         = model.BusinessName,
            BusinessType         = model.BusinessType,
            Location             = model.Location,
            Phone                = model.Phone,
            Email                = model.Email,
            Reason               = model.Reason,
            BankAccountName      = model.BankAccountName,
            BankAccountNumber    = model.BankAccountNumber,
            BankName             = model.BankName,
            BankCode             = model.BankCode,
            Status               = model.Status,
            RejectionReason      = model.RejectionReason,
            RejectionCount       = model.RejectionCount,
            CooldownUntil        = model.CooldownUntil,
            IsPermanentlyRejected = model.IsPermanentlyRejected,
            ContactLogs          = model.ContactLogs
                .Select(ApplicationContactLogViewModel.FromModel)
                .ToList(),
            ReviewedBy           = model.ReviewedBy,
            ReviewedAt           = model.ReviewedAt,
            SubmittedAt          = model.SubmittedAt,
            CreatedAt            = model.CreatedAt,
            UpdatedAt            = model.UpdatedAt
        };
    }

    public static List<PartnerApplicationViewModel> FromCloudModels(
        IEnumerable<PartnerApplicationModel> models)
    {
        if (models == null) return new List<PartnerApplicationViewModel>();
        return models.Select(FromCloudModel).ToList();
    }
}

/// <summary>
/// View model for a single admin contact log entry.
/// </summary>
public class ApplicationContactLogViewModel
{
    public string LogId { get; set; } = string.Empty;
    public string LoggedBy { get; set; } = string.Empty;
    public string LoggedByName { get; set; } = string.Empty;
    public DateTime LoggedAt { get; set; }
    public string ContactMethod { get; set; } = string.Empty;
    public string ContactDate { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;

    public string OutcomeBadgeClass => Outcome switch
    {
        ContactOutcome.Positive   => "badge-success",
        ContactOutcome.Negative   => "badge-danger",
        ContactOutcome.Neutral    => "badge-secondary",
        ContactOutcome.NoResponse => "badge-warning",
        _                         => "badge-secondary"
    };

    public string LoggedAtDisplay =>
        LoggedAt.ToString("MMM dd, yyyy HH:mm");

    public static ApplicationContactLogViewModel FromModel(ApplicationContactLog model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        return new ApplicationContactLogViewModel
        {
            LogId         = model.LogId,
            LoggedBy      = model.LoggedBy,
            LoggedByName  = model.LoggedByName,
            LoggedAt      = model.LoggedAt,
            ContactMethod = model.ContactMethod,
            ContactDate   = model.ContactDate,
            Notes         = model.Notes,
            Outcome       = model.Outcome
        };
    }
}

/// <summary>
/// Form data used when a user submits a partner application.
/// Validated in the service layer before being saved.
/// </summary>
public class SubmitPartnerApplicationRequest
{
    public string FullName { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string BusinessType { get; set; } = string.Empty;

    /// <summary>Must be "Abuja" or "Kaduna".</summary>
    public string Location { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>Minimum 50 characters.</summary>
    public string Reason { get; set; } = string.Empty;

    public string BankAccountName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string? BankCode { get; set; }
}

/// <summary>
/// Request data when admin adds a contact log entry.
/// </summary>
public class AddContactLogRequest
{
    public string ApplicationId { get; set; } = string.Empty;
    public string AdminUserId { get; set; } = string.Empty;
    public string AdminName { get; set; } = string.Empty;
    public string ContactMethod { get; set; } = string.Empty;
    public string ContactDate { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
}

/// <summary>
/// Request data when admin approves or rejects an application.
/// </summary>
public class ReviewApplicationRequest
{
    public string ApplicationId { get; set; } = string.Empty;
    public string AdminUserId { get; set; } = string.Empty;

    /// <summary>
    /// true = approve, false = reject.
    /// </summary>
    public bool Approve { get; set; }

    /// <summary>
    /// Required if Approve is false.
    /// </summary>
    public string? RejectionReason { get; set; }

    /// <summary>
    /// Override cooldown duration. Default logic:
    /// rejection 1 = 30 days, rejection 2 = 90 days, rejection 3 = permanent.
    /// </summary>
    public int? CooldownDaysOverride { get; set; }
}
