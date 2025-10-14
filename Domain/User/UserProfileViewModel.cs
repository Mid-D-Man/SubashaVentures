namespace SubashaVentures.Domain.User;

/// <summary>
/// View model for user profile information
/// </summary>
public class UserProfileViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    
    // Personal Info
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    
    // Profile
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    
    // Account Status
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }
    public string AccountStatus { get; set; } = "Active"; // Active, Suspended, Deleted
    
    // Preferences
    public bool EmailNotifications { get; set; } = true;
    public bool SmsNotifications { get; set; } = false;
    public string PreferredLanguage { get; set; } = "en";
    public string Currency { get; set; } = "NGN";
    
    // Statistics
    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }
    public int WishlistCount { get; set; }
    public int ReviewsCount { get; set; }
    
    // Loyalty/Rewards (if applicable)
    public int LoyaltyPoints { get; set; }
    public MembershipTier MembershipTier { get; set; } = MembershipTier.Bronze;
    
    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Computed properties
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string DisplayName => string.IsNullOrEmpty(FullName) ? Email : FullName;
    public string Initials => $"{FirstName.FirstOrDefault()}{LastName.FirstOrDefault()}".ToUpper();
    public bool IsVerified => IsEmailVerified;
    public string MemberSince => CreatedAt.ToString("MMMM yyyy");
}
