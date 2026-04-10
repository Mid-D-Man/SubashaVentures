using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;

namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Maps to partner_payout_requests table.
/// Partner withdrawal requests. Manual process — admin pays via
/// bank transfer then marks the request as paid.
/// Minimum withdrawal: ₦10,000.
/// </summary>
[Table("partner_payout_requests")]
public class PartnerPayoutRequestModel : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    // ── Ownership ──────────────────────────────────────────────
    [Column("partner_id")]
    public Guid PartnerId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    // ── Request Details ────────────────────────────────────────
    [Column("amount_requested")]
    public decimal AmountRequested { get; set; }

    // ── Bank Details Snapshot ──────────────────────────────────
    [Column("bank_account_name")]
    public string BankAccountName { get; set; } = string.Empty;

    [Column("bank_account_number")]
    public string BankAccountNumber { get; set; } = string.Empty;

    [Column("bank_name")]
    public string BankName { get; set; } = string.Empty;

    [Column("bank_code")]
    public string? BankCode { get; set; }

    // ── Status ─────────────────────────────────────────────────
    [Column("status")]
    public string Status { get; set; } = PayoutRequestStatus.Pending;

    // ── Admin Processing ───────────────────────────────────────
    [Column("transaction_reference")]
    public string? TransactionReference { get; set; }

    [Column("payment_method")]
    public string PaymentMethod { get; set; } = "bank_transfer";

    [Column("admin_notes")]
    public string? AdminNotes { get; set; }

    [Column("processed_by")]
    public string? ProcessedBy { get; set; }

    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    [Column("rejection_reason")]
    public string? RejectionReason { get; set; }

    // ── Timestamps ────────────────────────────────────────────
    [Column("requested_at")]
    public DateTime RequestedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // ── Computed Helpers (not mapped) ──────────────────────────
    [JsonIgnore]
    public string DisplayAmount => $"₦{AmountRequested:N0}";

    [JsonIgnore]
    public bool IsPending    => Status == PayoutRequestStatus.Pending;

    [JsonIgnore]
    public bool IsProcessing => Status == PayoutRequestStatus.Processing;

    [JsonIgnore]
    public bool IsPaid       => Status == PayoutRequestStatus.Paid;

    [JsonIgnore]
    public bool IsRejected   => Status == PayoutRequestStatus.Rejected;

    [JsonIgnore]
    public bool IsTerminal   => IsPaid || IsRejected;

    [JsonIgnore]
    public bool CanCancel    => IsPending;

    [JsonIgnore]
    public string MaskedAccountNumber =>
        BankAccountNumber.Length >= 4
            ? string.Concat(
                new string('*', BankAccountNumber.Length - 4),
                BankAccountNumber[^4..])
            : BankAccountNumber;
}

/// <summary>
/// Valid status values for payout requests.
/// </summary>
public static class PayoutRequestStatus
{
    public const string Pending    = "pending";
    public const string Processing = "processing";
    public const string Paid       = "paid";
    public const string Rejected   = "rejected";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Pending, Processing, Paid, Rejected
    };

    public static readonly IReadOnlyList<string> Open = new[]
    {
        Pending, Processing
    };

    public static readonly IReadOnlyList<string> Terminal = new[]
    {
        Paid, Rejected
    };
}

/// <summary>
/// Minimum payout threshold in Naira.
/// Matches the CHECK constraint in the migration.
/// </summary>
public static class PayoutConstants
{
    public const decimal MinimumPayoutAmount = 10_000.00m;
    public const string  DefaultPaymentMethod = "bank_transfer";
}
