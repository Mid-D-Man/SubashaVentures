using SubashaVentures.Models.Supabase;

namespace SubashaVentures.Domain.Partner;

/// <summary>
/// View model for a single payout request.
/// </summary>
public class PartnerPayoutRequestViewModel
{
    public string Id { get; set; } = string.Empty;
    public string PartnerId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;

    public decimal AmountRequested { get; set; }

    // ── Bank Snapshot ─────────────────────────────────────────
    public string BankAccountName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string? BankCode { get; set; }

    // ── Status ─────────────────────────────────────────────────
    public string Status { get; set; } = PayoutRequestStatus.Pending;

    // ── Admin Fields ──────────────────────────────────────────
    public string? TransactionReference { get; set; }
    public string? PaymentMethod { get; set; }
    public string? AdminNotes { get; set; }
    public string? ProcessedBy { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? RejectionReason { get; set; }

    // ── Timestamps ────────────────────────────────────────────
    public DateTime RequestedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // ── Computed ──────────────────────────────────────────────
    public string DisplayAmount => $"₦{AmountRequested:N0}";
    public bool IsPending       => Status == PayoutRequestStatus.Pending;
    public bool IsProcessing    => Status == PayoutRequestStatus.Processing;
    public bool IsPaid          => Status == PayoutRequestStatus.Paid;
    public bool IsRejected      => Status == PayoutRequestStatus.Rejected;
    public bool IsTerminal      => IsPaid || IsRejected;
    public bool CanCancel       => IsPending;

    public string StatusDisplay => Status switch
    {
        PayoutRequestStatus.Pending    => "Pending",
        PayoutRequestStatus.Processing => "Processing",
        PayoutRequestStatus.Paid       => "Paid",
        PayoutRequestStatus.Rejected   => "Rejected",
        _                              => Status
    };

    public string StatusBadgeClass => Status switch
    {
        PayoutRequestStatus.Pending    => "badge-warning",
        PayoutRequestStatus.Processing => "badge-info",
        PayoutRequestStatus.Paid       => "badge-success",
        PayoutRequestStatus.Rejected   => "badge-danger",
        _                              => "badge-secondary"
    };

    public string MaskedAccountNumber =>
        BankAccountNumber.Length >= 4
            ? string.Concat(
                new string('*', BankAccountNumber.Length - 4),
                BankAccountNumber[^4..])
            : BankAccountNumber;

    public string RequestedAtDisplay =>
        RequestedAt.ToString("MMM dd, yyyy HH:mm");

    public string ProcessedAtDisplay =>
        ProcessedAt?.ToString("MMM dd, yyyy HH:mm") ?? "—";

    public static PartnerPayoutRequestViewModel FromCloudModel(
        PartnerPayoutRequestModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        return new PartnerPayoutRequestViewModel
        {
            Id                   = model.Id.ToString(),
            PartnerId            = model.PartnerId.ToString(),
            UserId               = model.UserId.ToString(),
            AmountRequested      = model.AmountRequested,
            BankAccountName      = model.BankAccountName,
            BankAccountNumber    = model.BankAccountNumber,
            BankName             = model.BankName,
            BankCode             = model.BankCode,
            Status               = model.Status,
            TransactionReference = model.TransactionReference,
            PaymentMethod        = model.PaymentMethod,
            AdminNotes           = model.AdminNotes,
            ProcessedBy          = model.ProcessedBy,
            ProcessedAt          = model.ProcessedAt,
            RejectionReason      = model.RejectionReason,
            RequestedAt          = model.RequestedAt,
            CreatedAt            = model.CreatedAt,
            UpdatedAt            = model.UpdatedAt
        };
    }

    public static List<PartnerPayoutRequestViewModel> FromCloudModels(
        IEnumerable<PartnerPayoutRequestModel> models)
    {
        if (models == null) return new List<PartnerPayoutRequestViewModel>();
        return models.Select(FromCloudModel).ToList();
    }
}

/// <summary>
/// View model for a single bank update request.
/// </summary>
public class PartnerBankUpdateRequestViewModel
{
    public string Id { get; set; } = string.Empty;
    public string PartnerId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;

    public string NewAccountName { get; set; } = string.Empty;
    public string NewAccountNumber { get; set; } = string.Empty;
    public string NewBankName { get; set; } = string.Empty;
    public string? NewBankCode { get; set; }

    public string? OldAccountName { get; set; }
    public string? OldAccountNumber { get; set; }
    public string? OldBankName { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = BankUpdateRequestStatus.Pending;

    public string? AdminNotes { get; set; }
    public string? RejectionReason { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public DateTime RequestedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public bool IsPending  => Status == BankUpdateRequestStatus.Pending;
    public bool IsApproved => Status == BankUpdateRequestStatus.Approved;
    public bool IsRejected => Status == BankUpdateRequestStatus.Rejected;

    public string StatusDisplay => Status switch
    {
        BankUpdateRequestStatus.Pending  => "Pending Approval",
        BankUpdateRequestStatus.Approved => "Approved",
        BankUpdateRequestStatus.Rejected => "Rejected",
        _                                => Status
    };

    public string StatusBadgeClass => Status switch
    {
        BankUpdateRequestStatus.Pending  => "badge-warning",
        BankUpdateRequestStatus.Approved => "badge-success",
        BankUpdateRequestStatus.Rejected => "badge-danger",
        _                                => "badge-secondary"
    };

    public string MaskedNewAccountNumber =>
        NewAccountNumber.Length >= 4
            ? string.Concat(
                new string('*', NewAccountNumber.Length - 4),
                NewAccountNumber[^4..])
            : NewAccountNumber;

    public string RequestedAtDisplay =>
        RequestedAt.ToString("MMM dd, yyyy HH:mm");

    public static PartnerBankUpdateRequestViewModel FromCloudModel(
        PartnerBankUpdateRequestModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        return new PartnerBankUpdateRequestViewModel
        {
            Id               = model.Id.ToString(),
            PartnerId        = model.PartnerId.ToString(),
            UserId           = model.UserId.ToString(),
            NewAccountName   = model.NewAccountName,
            NewAccountNumber = model.NewAccountNumber,
            NewBankName      = model.NewBankName,
            NewBankCode      = model.NewBankCode,
            OldAccountName   = model.OldAccountName,
            OldAccountNumber = model.OldAccountNumber,
            OldBankName      = model.OldBankName,
            Reason           = model.Reason,
            Status           = model.Status,
            AdminNotes       = model.AdminNotes,
            RejectionReason  = model.RejectionReason,
            ReviewedBy       = model.ReviewedBy,
            ReviewedAt       = model.ReviewedAt,
            RequestedAt      = model.RequestedAt,
            CreatedAt        = model.CreatedAt,
            UpdatedAt        = model.UpdatedAt
        };
    }
}

/// <summary>
/// Earnings summary for the partner financials dashboard.
/// Aggregated at the service layer — not a DB model.
/// </summary>
public class PartnerEarningsSummary
{
    public string PartnerId { get; set; } = string.Empty;

    // ── Current Balances ──────────────────────────────────────
    public decimal TotalRevenue { get; set; }
    public decimal TotalCommissionPaid { get; set; }
    public decimal PendingPayout { get; set; }
    public decimal TotalWithdrawn { get; set; }

    // ── This Month ─────────────────────────────────────────────
    public decimal ThisMonthRevenue { get; set; }
    public decimal ThisMonthOrders { get; set; }

    // ── Payout Status ─────────────────────────────────────────
    public bool CanRequestPayout =>
        PendingPayout >= PayoutConstants.MinimumPayoutAmount;

    public decimal AmountUntilThreshold =>
        Math.Max(0, PayoutConstants.MinimumPayoutAmount - PendingPayout);

    public bool HasOpenPayoutRequest { get; set; }

    // ── Recent Requests ───────────────────────────────────────
    public List<PartnerPayoutRequestViewModel> RecentPayoutRequests { get; set; } = new();

    // ── Bank Details ──────────────────────────────────────────
    public string? CurrentBankAccountName { get; set; }
    public string? CurrentBankAccountNumber { get; set; }
    public string? CurrentBankName { get; set; }
    public bool HasPendingBankUpdateRequest { get; set; }

    // ── Computed Display ───────────────────────────────────────
    public string DisplayTotalRevenue        => $"₦{TotalRevenue:N0}";
    public string DisplayPendingPayout       => $"₦{PendingPayout:N0}";
    public string DisplayTotalWithdrawn      => $"₦{TotalWithdrawn:N0}";
    public string DisplayThisMonthRevenue    => $"₦{ThisMonthRevenue:N0}";
    public string DisplayAmountUntilThreshold => $"₦{AmountUntilThreshold:N0}";
    public string DisplayMinimumThreshold    => $"₦{PayoutConstants.MinimumPayoutAmount:N0}";

    public decimal PayoutProgressPercent =>
        PayoutConstants.MinimumPayoutAmount <= 0
            ? 100
            : Math.Min(100,
                Math.Round(PendingPayout / PayoutConstants.MinimumPayoutAmount * 100, 1));

    public string MaskedCurrentAccountNumber
    {
        get
        {
            if (string.IsNullOrEmpty(CurrentBankAccountNumber)) return string.Empty;
            return CurrentBankAccountNumber.Length >= 4
                ? string.Concat(
                    new string('*', CurrentBankAccountNumber.Length - 4),
                    CurrentBankAccountNumber[^4..])
                : CurrentBankAccountNumber;
        }
    }
}

/// <summary>
/// Request to submit a payout withdrawal request.
/// </summary>
public class RequestPayoutRequest
{
    public string PartnerId { get; set; } = string.Empty;
    public decimal AmountRequested { get; set; }
}

/// <summary>
/// Request when admin marks a payout as paid.
/// </summary>
public class MarkPayoutPaidRequest
{
    public string PayoutRequestId { get; set; } = string.Empty;
    public string AdminUserId { get; set; } = string.Empty;
    public string TransactionReference { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = "bank_transfer";
    public string? AdminNotes { get; set; }
}

/// <summary>
/// Request to submit a bank detail update.
/// </summary>
public class RequestBankUpdateRequest
{
    public string PartnerId { get; set; } = string.Empty;
    public string NewAccountName { get; set; } = string.Empty;
    public string NewAccountNumber { get; set; } = string.Empty;
    public string NewBankName { get; set; } = string.Empty;
    public string? NewBankCode { get; set; }
    public string Reason { get; set; } = string.Empty;
}
