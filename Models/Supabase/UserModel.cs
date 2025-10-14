namespace SubashaVentures.Models.Supabase;

/// <summary>
/// Supabase user model - linked to auth.users
/// </summary>
public record UserModel : ISecureEntity
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public string? Gender { get; init; }
    public string? AvatarUrl { get; init; }
    public bool IsEmailVerified { get; init; }
    public bool IsPhoneVerified { get; init; }
    public string AccountStatus { get; init; } = "Active";
    
    // Preferences
    public bool EmailNotifications { get; init; } = true;
    public bool SmsNotifications { get; init; } = false;
    public string PreferredLanguage { get; init; } = "en";
    public string Currency { get; init; } = "NGN";
    
    // Statistics
    public int TotalOrders { get; init; }
    public decimal TotalSpent { get; init; }
    public int LoyaltyPoints { get; init; }
    public string MembershipTier { get; init; } = "Bronze";
    
    // ISecureEntity
    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = "system";
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime? DeletedAt { get; init; }
    public string? DeletedBy { get; init; }
    public DateTime? LastLoginAt { get; init; }
}
