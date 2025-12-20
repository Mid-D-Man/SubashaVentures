// Domain/User/UserProfileViewModel.cs - UPDATED FOR ROLES
namespace SubashaVentures.Domain.User;

/// <summary>
/// View model for user profile information - matches Supabase Auth + extended profile
/// Updated: Added Roles property for role-based authorization
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
    
    // ==================== NEW: ROLES ====================
    
    /// <summary>
    /// User roles from public.user_roles table
    /// Examples: "user", "superior_admin"
    /// </summary>
    public List<string> Roles { get; set; } = new();
    
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
    
    // ==================== NEW: ROLE-BASED PROPERTIES ====================
    
    /// <summary>
    /// Check if user is superior admin
    /// </summary>
    public bool IsSuperiorAdmin => Roles.Contains("superior_admin", StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// Check if user is regular user
    /// </summary>
    public bool IsRegularUser => Roles.Contains("user", StringComparer.OrdinalIgnoreCase) || Roles.Count == 0;
    
    /// <summary>
    /// Get roles as display string (e.g., "user, superior_admin")
    /// </summary>
    public string RoleDisplay => Roles.Any() ? string.Join(", ", Roles) : "user";
    
    /// <summary>
    /// Check if user has a specific role
    /// </summary>
    public bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// Get all role badges for UI display
    /// </summary>
    public List<RoleBadge> RoleBadges => Roles.Select(role => new RoleBadge
    {
        Role = role,
        DisplayName = role switch
        {
            "superior_admin" => "Superior Admin",
            "user" => "User",
            _ => role
        },
        BadgeClass = role switch
        {
            "superior_admin" => "badge-danger",
            "user" => "badge-info",
            _ => "badge-secondary"
        }
    }).ToList();
}

/// <summary>
/// Role badge for UI display
/// </summary>
public class RoleBadge
{
    public string Role { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string BadgeClass { get; set; } = "badge-secondary";
}
