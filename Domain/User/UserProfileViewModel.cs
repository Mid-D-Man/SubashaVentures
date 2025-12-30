namespace SubashaVentures.Domain.User;

using SubashaVentures.Models.Supabase;

/// <summary>
/// View model for user profile information
/// UPDATED: Role is now a single field, not a list
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
    
    // ==================== ROLE (SINGLE FIELD) ====================
    
    /// <summary>
    /// User role: 'user' or 'superior_admin'
    /// </summary>
    public string Role { get; set; } = "user";
    
    // ==================== COMPUTED PROPERTIES ====================
    
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string DisplayName => string.IsNullOrEmpty(FullName) ? Email : FullName;
    public string Initials => $"{FirstName.FirstOrDefault()}{LastName.FirstOrDefault()}".ToUpper();
    public bool IsVerified => EmailVerified;
    public bool IsEmailVerified => EmailVerified;
    public bool IsPhoneVerified => PhoneVerified;
    public string MemberSince => CreatedAt.ToString("MMMM yyyy");
    public string DisplayTotalSpent => $"₦{TotalSpent:N0}";
    public string DisplayLoyaltyPoints => LoyaltyPoints.ToString("N0");
    
    public bool IsSuperiorAdmin => Role.Equals("superior_admin", StringComparison.OrdinalIgnoreCase);
    public bool IsRegularUser => Role.Equals("user", StringComparison.OrdinalIgnoreCase);
    public string RoleDisplay => Role == "superior_admin" ? "Superior Admin" : "User";
    
    public bool HasRole(string role) => Role.Equals(role, StringComparison.OrdinalIgnoreCase);
    
    public RoleBadge RoleBadge => new RoleBadge
    {
        Role = Role,
        DisplayName = Role == "superior_admin" ? "Superior Admin" : "User",
        BadgeClass = Role == "superior_admin" ? "badge-danger" : "badge-info"
    };
    
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
            Bio = model.Bio,
            AccountStatus = model.AccountStatus,
            SuspensionReason = model.SuspensionReason,
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
            Role = model.Role
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
            Bio = this.Bio,
            IsEmailVerified = this.EmailVerified,
            IsPhoneVerified = this.PhoneVerified,
            AccountStatus = this.AccountStatus,
            SuspensionReason = this.SuspensionReason,
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
            LastLoginAt = this.LastLoginAt,
            Role = this.Role
        };
    }
}

public class RoleBadge
{
    public string Role { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string BadgeClass { get; set; } = "badge-secondary";
}
