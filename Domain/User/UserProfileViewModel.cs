// Domain/User/UserProfileViewModel.cs - UPDATED FOR ROLES WITH CONVERSION
namespace SubashaVentures.Domain.User;

using SubashaVentures.Models.Supabase;

/// <summary>
/// View model for user profile information - matches Supabase Auth + extended profile
/// Updated: Added Roles property for role-based authorization
/// </summary>
public class UserProfileViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime? EmailConfirmedAt { get; set; }
    public DateTime? BannedUntil { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSignInAt { get; set; }
    public bool IsAnonymous { get; set; }
    public bool IsSsoUser { get; set; }
    
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? PhoneNumber { get; set; }
    public bool EmailVerified { get; set; }
    public bool PhoneVerified { get; set; }
    
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Bio { get; set; }
    
    public string AccountStatus { get; set; } = "Active";
    public string? SuspensionReason { get; set; }
    
    public bool EmailNotifications { get; set; } = true;
    public bool SmsNotifications { get; set; } = false;
    public string PreferredLanguage { get; set; } = "en";
    public string Currency { get; set; } = "NGN";
    
    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }
    public int WishlistCount { get; set; }
    public int ReviewsCount { get; set; }
    public int LoyaltyPoints { get; set; }
    public MembershipTier MembershipTier { get; set; } = MembershipTier.Bronze;
    
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    
    public List<string> Roles { get; set; } = new();
    
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string DisplayName => string.IsNullOrEmpty(FullName) ? Email : FullName;
    public string Initials => $"{FirstName.FirstOrDefault()}{LastName.FirstOrDefault()}".ToUpper();
    public bool IsVerified => EmailVerified;
    public bool IsEmailVerified => EmailVerified;
    public bool IsPhoneVerified => PhoneVerified;
    public string MemberSince => CreatedAt.ToString("MMMM yyyy");
    public string DisplayTotalSpent => $"₦{TotalSpent:N0}";
    public string DisplayLoyaltyPoints => LoyaltyPoints.ToString("N0");
    
    public bool IsSuperiorAdmin => Roles.Contains("superior_admin", StringComparer.OrdinalIgnoreCase);
    public bool IsRegularUser => Roles.Contains("user", StringComparer.OrdinalIgnoreCase) || Roles.Count == 0;
    public string RoleDisplay => Roles.Any() ? string.Join(", ", Roles) : "user";
    
    public bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    
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
    
    // ==================== CONVERSION METHODS ====================
    
    /// <summary>
    /// Convert from Supabase UserModel to UserProfileViewModel
    /// </summary>
    public static UserProfileViewModel FromCloudModel(UserModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
            
        var tierEnum = MembershipTier.Bronze;
        if (Enum.TryParse<MembershipTier>(model.MembershipTier, true, out var parsedTier))
        {
            tierEnum = parsedTier;
        }
            
        return new UserProfileViewModel
        {
            Id = model.Id,
            Email = model.Email,
            EmailConfirmedAt = null,
            BannedUntil = null,
            CreatedAt = model.CreatedAt,
            LastSignInAt = model.LastLoginAt,
            IsAnonymous = false,
            IsSsoUser = false,
            FirstName = model.FirstName,
            LastName = model.LastName,
            AvatarUrl = model.AvatarUrl,
            PhoneNumber = model.PhoneNumber,
            EmailVerified = model.IsEmailVerified,
            PhoneVerified = model.IsPhoneVerified,
            DateOfBirth = model.DateOfBirth,
            Gender = model.Gender,
            Bio = null,
            AccountStatus = model.AccountStatus,
            SuspensionReason = null,
            EmailNotifications = model.EmailNotifications,
            SmsNotifications = model.SmsNotifications,
            PreferredLanguage = model.PreferredLanguage,
            Currency = model.Currency,
            TotalOrders = model.TotalOrders,
            TotalSpent = model.TotalSpent,
            WishlistCount = 0,
            ReviewsCount = 0,
            LoyaltyPoints = model.LoyaltyPoints,
            MembershipTier = tierEnum,
            UpdatedAt = model.UpdatedAt,
            LastLoginAt = model.LastLoginAt,
            Roles = model.RoleStrings
        };
    }
    
    /// <summary>
    /// Convert from UserProfileViewModel to Supabase UserModel
    /// </summary>
    public UserModel ToCloudModel()
    {
        return new UserModel
        {
            Id = this.Id,
            Email = this.Email,
            FirstName = this.FirstName,
            LastName = this.LastName,
            PhoneNumber = this.PhoneNumber,
            DateOfBirth = this.DateOfBirth,
            Gender = this.Gender,
            AvatarUrl = this.AvatarUrl,
            IsEmailVerified = this.EmailVerified,
            IsPhoneVerified = this.PhoneVerified,
            AccountStatus = this.AccountStatus,
            EmailNotifications = this.EmailNotifications,
            SmsNotifications = this.SmsNotifications,
            PreferredLanguage = this.PreferredLanguage,
            Currency = this.Currency,
            TotalOrders = this.TotalOrders,
            TotalSpent = this.TotalSpent,
            LoyaltyPoints = this.LoyaltyPoints,
            MembershipTier = this.MembershipTier.ToString(),
            CreatedAt = this.CreatedAt,
            CreatedBy = "system",
            UpdatedAt = this.UpdatedAt,
            LastLoginAt = this.LastLoginAt
        };
    }
}

public class RoleBadge
{
    public string Role { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string BadgeClass { get; set; } = "badge-secondary";
}
