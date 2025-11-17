// Domain/User/UserProfileViewModel.cs - UPDATED FOR SUPABASE AUTH
namespace SubashaVentures.Domain.User;

/// <summary>
/// View model for user profile information - matches Supabase Auth + extended profile
/// </summary>
public class UserProfileViewModel
{
    // ==================== FROM auth.users (Supabase Auth) ====================
    public string Id { get; set; } = string.Empty; // UUID from auth.users.id
    public string Email { get; set; } = string.Empty;
    public DateTime? EmailConfirmedAt { get; set; }
    public DateTime? BannedUntil { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSignInAt { get; set; }
    public bool IsAnonymous { get; set; }
    public bool IsSsoUser { get; set; }
    
    // ==================== FROM raw_user_meta_data (JSONB in auth.users) ====================
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? PhoneNumber { get; set; }
    public bool EmailVerified { get; set; }
    public bool PhoneVerified { get; set; }
    
    // ==================== FROM public.users (Extended Profile) ====================
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Bio { get; set; }
    
    // Account Status (NOT in Supabase Auth - custom field in public.users)
    public string AccountStatus { get; set; } = "Active"; // Active, Suspended, Deleted
    public string? SuspensionReason { get; set; }
    
    // Preferences
    public bool EmailNotifications { get; set; } = true;
    public bool SmsNotifications { get; set; } = false;
    public string PreferredLanguage { get; set; } = "en";
    public string Currency { get; set; } = "NGN";
    
    // Statistics (from public.users)
    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }
    public int WishlistCount { get; set; }
    public int ReviewsCount { get; set; }
    public int LoyaltyPoints { get; set; }
    public MembershipTier MembershipTier { get; set; } = MembershipTier.Bronze;
    
    // Timestamps
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    
    // ==================== COMPUTED PROPERTIES ====================
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string DisplayName => string.IsNullOrEmpty(FullName) ? Email : FullName;
    public string Initials => $"{FirstName.FirstOrDefault()}{LastName.FirstOrDefault()}".ToUpper();
    public bool IsVerified => EmailVerified;
    public bool IsEmailVerified => EmailVerified; // Alias for consistency
    public bool IsPhoneVerified => PhoneVerified; // Alias for consistency
    public string MemberSince => CreatedAt.ToString("MMMM yyyy");
    public string DisplayTotalSpent => $"₦{TotalSpent:N0}";
    public string DisplayLoyaltyPoints => LoyaltyPoints.ToString("N0");
}
