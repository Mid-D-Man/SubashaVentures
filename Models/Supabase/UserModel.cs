// Models/Supabase/UserModel.cs - UPDATED WITH NICKNAME + JsonIgnore on computed props
using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Supabase user_data model - linked to auth.users
/// UPDATED: Added nickname field + [JsonIgnore] on computed properties
/// </summary>
[Table("user_data")]
public class UserModel : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public string Id { get; set; } = string.Empty;
    
    [Column("email")]
    public string Email { get; set; } = string.Empty;
    
    [Column("first_name")]
    public string FirstName { get; set; } = string.Empty;
    
    [Column("last_name")]
    public string LastName { get; set; } = string.Empty;
    
    [Column("nickname")]
    public string? Nickname { get; set; }
    
    [Column("phone_number")]
    public string? PhoneNumber { get; set; }
    
    [Column("date_of_birth")]
    public DateTime? DateOfBirth { get; set; }
    
    [Column("gender")]
    public string? Gender { get; set; }
    
    [Column("avatar_url")]
    public string? AvatarUrl { get; set; }
    
    [Column("bio")]
    public string? Bio { get; set; }
    
    [Column("is_email_verified")]
    public bool IsEmailVerified { get; set; }
    
    [Column("is_phone_verified")]
    public bool IsPhoneVerified { get; set; }
    
    [Column("account_status")]
    public string AccountStatus { get; set; } = "Active";
    
    [Column("suspension_reason")]
    public string? SuspensionReason { get; set; }
    
    // Preferences
    [Column("email_notifications")]
    public bool EmailNotifications { get; set; } = true;
    
    [Column("sms_notifications")]
    public bool SmsNotifications { get; set; } = false;
    
    [Column("preferred_language")]
    public string PreferredLanguage { get; set; } = "en";
    
    [Column("currency")]
    public string Currency { get; set; } = "NGN";
    
    // Statistics
    [Column("total_orders")]
    public int TotalOrders { get; set; }
    
    [Column("total_spent")]
    public decimal TotalSpent { get; set; }
    
    [Column("loyalty_points")]
    public int LoyaltyPoints { get; set; }
    
    [Column("membership_tier")]
    public string MembershipTier { get; set; } = "Bronze";
    
    [Column("role")]
    public string Role { get; set; } = "user";
    
    // ISecureEntity
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("created_by")]
    public string CreatedBy { get; set; } = "system";
    
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
    
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }
    
    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
    
    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }
    
    [Column("deleted_by")]
    public string? DeletedBy { get; set; }
    
    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }
    
    // ──────────────────────────────────────────────────────────────────────────
    // COMPUTED / HELPER PROPERTIES
    // [JsonIgnore] prevents Postgrest from trying to PATCH these non-existent columns.
    // ──────────────────────────────────────────────────────────────────────────

   // [JsonIgnore]
    public bool HasRole(string role) => Role.Equals(role, StringComparison.OrdinalIgnoreCase);
    
  //  [JsonIgnore]
    public bool IsSuperiorAdmin => Role.Equals("superior_admin", StringComparison.OrdinalIgnoreCase);
    
  //  [JsonIgnore]
    public bool IsRegularUser => Role.Equals("user", StringComparison.OrdinalIgnoreCase);
    
    /// <summary>
    /// Display name with nickname support.
    /// [JsonIgnore] — this is computed; there is no 'DisplayName' column in user_data.
    /// </summary>
   // [JsonIgnore]
    public string DisplayName => !string.IsNullOrWhiteSpace(Nickname)
        ? Nickname
        : $"{FirstName} {LastName}".Trim();
}
