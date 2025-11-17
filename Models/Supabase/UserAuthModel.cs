// Models/Supabase/UserAuthModel.cs - NEW
// Maps directly to auth.users table (accessed via Admin API)
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Maps to Supabase auth.users table
/// Note: This is READ-ONLY for most operations - auth changes should go through Supabase Auth API
/// </summary>
[Table("users")] // Note: Supabase exposes auth.users as "users" in some contexts
public class UserAuthModel : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public string Id { get; set; } = string.Empty;
    
    [Column("email")]
    public string Email { get; set; } = string.Empty;
    
    [Column("banned_until")]
    public DateTime? BannedUntil { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("confirmed_at")]
    public DateTime? ConfirmedAt { get; set; }
    
    [Column("confirmation_sent_at")]
    public DateTime? ConfirmationSentAt { get; set; }
    
    [Column("last_sign_in_at")]
    public DateTime? LastSignInAt { get; set; }
    
    [Column("is_anonymous")]
    public bool IsAnonymous { get; set; }
    
    [Column("is_sso_user")]
    public bool IsSsoUser { get; set; }
    
    [Column("invited_at")]
    public DateTime? InvitedAt { get; set; }
    
    [Column("phone")]
    public string? Phone { get; set; }
    
    // JSONB fields - stored as dictionaries
    [Column("raw_user_meta_data")]
    public Dictionary<string, object> RawUserMetaData { get; set; } = new();
    
    [Column("raw_app_meta_data")]
    public Dictionary<string, object> RawAppMetaData { get; set; } = new();
    
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
    
    [Column("providers")]
    public List<string> Providers { get; set; } = new();
    
    // Helper methods to safely extract metadata
    public string GetFirstName() => 
        RawUserMetaData.TryGetValue("first_name", out var val) ? val?.ToString() ?? "" : "";
    
    public string GetLastName() => 
        RawUserMetaData.TryGetValue("last_name", out var val) ? val?.ToString() ?? "" : "";
    
    public string GetAvatarUrl() => 
        RawUserMetaData.TryGetValue("avatar_url", out var val) ? val?.ToString() ?? "" : "";
    
    public string GetPhoneNumber() => 
        RawUserMetaData.TryGetValue("phone_number", out var val) ? val?.ToString() ?? "" : "";
    
    public bool GetEmailVerified() => 
        RawUserMetaData.TryGetValue("email_verified", out var val) && val is bool b && b;
    
    public bool GetPhoneVerified() => 
        RawUserMetaData.TryGetValue("phone_verified", out var val) && val is bool b && b;
}
