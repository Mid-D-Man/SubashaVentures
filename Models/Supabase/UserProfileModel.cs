// Models/Supabase/UserProfileModel.cs - UPDATED
// Maps to public.users table (extended profile beyond auth.users)
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Extended user profile stored in public.users
/// Links to auth.users via foreign key on id
/// </summary>
[Table("users")]
public class UserProfileModel : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public string Id { get; set; } = string.Empty; // FK to auth.users.id
    
    [Column("email")]
    public string Email { get; set; } = string.Empty;
    
    [Column("first_name")]
    public string FirstName { get; set; } = string.Empty;
    
    [Column("last_name")]
    public string LastName { get; set; } = string.Empty;
    
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
    public string AccountStatus { get; set; } = "Active"; // Active, Suspended, Deleted
    
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
    public string MembershipTier { get; set; } = "Bronze"; // Bronze, Silver, Gold, Platinum
    
    // Timestamps
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
    
    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }
    
    // Soft delete support (optional)
    [Column("is_deleted")]
    public bool IsDeleted { get; set; }
    
    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }
    
    [Column("deleted_by")]
    public string? DeletedBy { get; set; }
}
