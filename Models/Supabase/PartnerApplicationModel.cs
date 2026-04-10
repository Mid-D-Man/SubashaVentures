using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;

namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Maps to partner_applications table.
/// Partner/vendor onboarding applications submitted by users.
/// </summary>
[Table("partner_applications")]
public class PartnerApplicationModel : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    // ── User Link ──────────────────────────────────────────────
    [Column("user_id")]
    public Guid UserId { get; set; }

    // ── Applicant & Business Info ──────────────────────────────
    [Column("full_name")]
    public string FullName { get; set; } = string.Empty;

    [Column("business_name")]
    public string BusinessName { get; set; } = string.Empty;

    [Column("business_type")]
    public string BusinessType { get; set; } = string.Empty;

    [Column("location")]
    public string Location { get; set; } = string.Empty;

    [Column("phone")]
    public string Phone { get; set; } = string.Empty;

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    // ── Bank Details ───────────────────────────────────────────
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
    public string Status { get; set; } = "pending";

    // ── Rejection & Cooldown ───────────────────────────────────
    [Column("rejection_reason")]
    public string? RejectionReason { get; set; }

    [Column("rejection_count")]
    public short RejectionCount { get; set; } = 0;

    [Column("cooldown_until")]
    public DateTime? CooldownUntil { get; set; }

    [Column("is_permanently_rejected")]
    public bool IsPermanentlyRejected { get; set; } = false;

    // ── Contact Logs ───────────────────────────────────────────
    /// <summary>
    /// JSONB array of admin contact log entries.
    /// Each entry: { log_id, logged_by, logged_by_name, logged_at,
    ///               contact_method, contact_date, notes, outcome }
    /// </summary>
    [Column("contact_logs")]
    public List<ApplicationContactLog> ContactLogs { get; set; } = new();

    // ── Admin Audit ────────────────────────────────────────────
    [Column("reviewed_by")]
    public string? ReviewedBy { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    // ── Timestamps ────────────────────────────────────────────
    [Column("submitted_at")]
    public DateTime SubmittedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // ── Computed Helpers (not mapped) ──────────────────────────
    [JsonIgnore]
    public bool IsInCooldown =>
        CooldownUntil.HasValue && CooldownUntil.Value > DateTime.UtcNow;

    [JsonIgnore]
    public bool CanReapply =>
        !IsPermanentlyRejected
        && !IsInCooldown
        && Status is "rejected" or "pending";

    [JsonIgnore]
    public TimeSpan? CooldownRemaining =>
        IsInCooldown ? CooldownUntil!.Value - DateTime.UtcNow : null;

    [JsonIgnore]
    public string CooldownRemainingDisplay
    {
        get
        {
            if (!IsInCooldown || CooldownRemaining is null)
                return string.Empty;

            var ts = CooldownRemaining.Value;
            if (ts.TotalDays >= 1)
                return $"{(int)ts.TotalDays} day(s)";
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours} hour(s)";
            return $"{(int)ts.TotalMinutes} minute(s)";
        }
    }
}

/// <summary>
/// A single admin contact log entry stored in the contact_logs JSONB array.
/// </summary>
public class ApplicationContactLog
{
    [JsonProperty("log_id")]
    public string LogId { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("logged_by")]
    public string LoggedBy { get; set; } = string.Empty;

    [JsonProperty("logged_by_name")]
    public string LoggedByName { get; set; } = string.Empty;

    [JsonProperty("logged_at")]
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

    /// <summary>phone | email | in_person | whatsapp</summary>
    [JsonProperty("contact_method")]
    public string ContactMethod { get; set; } = string.Empty;

    /// <summary>Actual date contact was made (may differ from logged_at)</summary>
    [JsonProperty("contact_date")]
    public string ContactDate { get; set; } = string.Empty;

    [JsonProperty("notes")]
    public string Notes { get; set; } = string.Empty;

    /// <summary>positive | negative | neutral | no_response</summary>
    [JsonProperty("outcome")]
    public string Outcome { get; set; } = string.Empty;
}

/// <summary>
/// Valid status values for partner applications.
/// </summary>
public static class PartnerApplicationStatus
{
    public const string Pending              = "pending";
    public const string UnderReview          = "under_review";
    public const string Approved             = "approved";
    public const string Rejected             = "rejected";
    public const string PermanentlyRejected  = "permanently_rejected";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Pending, UnderReview, Approved, Rejected, PermanentlyRejected
    };

    public static readonly IReadOnlyList<string> Active = new[]
    {
        Pending, UnderReview
    };
}

/// <summary>
/// Valid contact methods for admin contact logs.
/// </summary>
public static class ContactMethod
{
    public const string Phone     = "phone";
    public const string Email     = "email";
    public const string InPerson  = "in_person";
    public const string WhatsApp  = "whatsapp";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Phone, Email, InPerson, WhatsApp
    };
}

/// <summary>
/// Valid outcomes for admin contact logs.
/// </summary>
public static class ContactOutcome
{
    public const string Positive   = "positive";
    public const string Negative   = "negative";
    public const string Neutral    = "neutral";
    public const string NoResponse = "no_response";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Positive, Negative, Neutral, NoResponse
    };
}

/// <summary>
/// Valid business types a partner can select.
/// Must match the CHECK constraint in the migration.
/// </summary>
public static class PartnerBusinessType
{
    public const string FashionApparel  = "Fashion & Apparel";
    public const string Electronics     = "Electronics";
    public const string HomeLiving      = "Home & Living";
    public const string HealthBeauty    = "Health & Beauty";
    public const string FoodBeverages   = "Food & Beverages";
    public const string SportsOutdoors  = "Sports & Outdoors";
    public const string BooksEducation  = "Books & Education";
    public const string Other           = "Other";

    public static readonly IReadOnlyList<string> All = new[]
    {
        FashionApparel, Electronics, HomeLiving, HealthBeauty,
        FoodBeverages, SportsOutdoors, BooksEducation, Other
    };
}

/// <summary>
/// Valid locations. Must match CHECK constraint in migration.
/// </summary>
public static class PartnerLocation
{
    public const string Abuja  = "Abuja";
    public const string Kaduna = "Kaduna";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Abuja, Kaduna
    };
}
