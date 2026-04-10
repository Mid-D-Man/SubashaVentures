using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;

namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Maps to partner_bank_update_requests table.
/// Partners submit bank detail change requests.
/// Admin must approve before the change takes effect.
/// Zero-trust: partners cannot self-update bank details.
/// </summary>
[Table("partner_bank_update_requests")]
public class PartnerBankUpdateRequestModel : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    // ── Ownership ──────────────────────────────────────────────
    [Column("partner_id")]
    public Guid PartnerId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    // ── New Bank Details ───────────────────────────────────────
    [Column("new_account_name")]
    public string NewAccountName { get; set; } = string.Empty;

    [Column("new_account_number")]
    public string NewAccountNumber { get; set; } = string.Empty;

    [Column("new_bank_name")]
    public string NewBankName { get; set; } = string.Empty;

    [Column("new_bank_code")]
    public string? NewBankCode { get; set; }

    // ── Old Bank Details Snapshot ──────────────────────────────
    [Column("old_account_name")]
    public string? OldAccountName { get; set; }

    [Column("old_account_number")]
    public string? OldAccountNumber { get; set; }

    [Column("old_bank_name")]
    public string? OldBankName { get; set; }

    // ── Reason ────────────────────────────────────────────────
    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    // ── Status ─────────────────────────────────────────────────
    [Column("status")]
    public string Status { get; set; } = BankUpdateRequestStatus.Pending;

    // ── Admin Fields ──────────────────────────────────────────
    [Column("admin_notes")]
    public string? AdminNotes { get; set; }

    [Column("rejection_reason")]
    public string? RejectionReason { get; set; }

    [Column("reviewed_by")]
    public string? ReviewedBy { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    // ── Timestamps ────────────────────────────────────────────
    [Column("requested_at")]
    public DateTime RequestedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // ── Computed Helpers (not mapped) ──────────────────────────
    [JsonIgnore]
    public bool IsPending  => Status == BankUpdateRequestStatus.Pending;

    [JsonIgnore]
    public bool IsApproved => Status == BankUpdateRequestStatus.Approved;

    [JsonIgnore]
    public bool IsRejected => Status == BankUpdateRequestStatus.Rejected;

    [JsonIgnore]
    public bool CanWithdraw => IsPending;

    [JsonIgnore]
    public string MaskedNewAccountNumber =>
        NewAccountNumber.Length >= 4
            ? string.Concat(
                new string('*', NewAccountNumber.Length - 4),
                NewAccountNumber[^4..])
            : NewAccountNumber;

    [JsonIgnore]
    public string MaskedOldAccountNumber
    {
        get
        {
            if (string.IsNullOrEmpty(OldAccountNumber)) return string.Empty;
            return OldAccountNumber.Length >= 4
                ? string.Concat(
                    new string('*', OldAccountNumber.Length - 4),
                    OldAccountNumber[^4..])
                : OldAccountNumber;
        }
    }
}

/// <summary>
/// Valid status values for bank update requests.
/// </summary>
public static class BankUpdateRequestStatus
{
    public const string Pending  = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Pending, Approved, Rejected
    };
}
