// Services/Admin/AdminUserSetupService.cs - COMPLETE IMPLEMENTATION
using SubashaVentures.Services.Users;
using SubashaVentures.Domain.User;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Admin;

public class AdminUserSetupService : IAdminUserSetupService
{
    private readonly IUserService _userService;
    private readonly Supabase.Client _supabaseClient;
    private readonly ILogger<AdminUserSetupService> _logger;

    // PREDEFINED ADMIN CREDENTIALS - NEVER CHANGE THESE IN PRODUCTION
    private const string ADMIN_EMAIL = "subashaventures.dev@gmail.com";
    private const string ADMIN_PASSWORD = "@SubashaAdmin#0307!";
    private const string ADMIN_FIRST_NAME = "SubashaVentures";
    private const string ADMIN_LAST_NAME = "Admin";

    public AdminUserSetupService(
        IUserService userService,
        Supabase.Client supabaseClient,
        ILogger<AdminUserSetupService> logger)
    {
        _userService = userService;
        _supabaseClient = supabaseClient;
        _logger = logger;
    }

    public async Task<bool> EnsureAdminUserExistsAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "═══════════════════════════════════════════",
                LogLevel.Info
            );
            await MID_HelperFunctions.DebugMessageAsync(
                "ADMIN USER SETUP - CHECKING...",
                LogLevel.Info
            );
            await MID_HelperFunctions.DebugMessageAsync(
                "═══════════════════════════════════════════",
                LogLevel.Info
            );

            // Check if admin user already exists
            var existingAdmin = await _userService.GetUserByEmailAsync(ADMIN_EMAIL);

            if (existingAdmin != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Admin user already exists",
                    LogLevel.Info
                );
                
                _logger.LogInformation("Admin User Status: EXISTS");
                _logger.LogInformation("Admin Email: {Email}", ADMIN_EMAIL);
                _logger.LogInformation("Admin ID: {UserId}", existingAdmin.Id);
                _logger.LogInformation("Account Status: {Status}", existingAdmin.AccountStatus);
                _logger.LogInformation("Membership Tier: {Tier}", existingAdmin.MembershipTier);
                
                // Ensure admin has proper role metadata
                await EnsureAdminRoleAsync(existingAdmin.Id);
                
                return true;
            }

            // Admin user doesn't exist - create it
            await MID_HelperFunctions.DebugMessageAsync(
                "⚠️ Admin user NOT found - Creating now...",
                LogLevel.Warning
            );

            var createRequest = new CreateUserRequest
            {
                Email = ADMIN_EMAIL,
                Password = ADMIN_PASSWORD,
                FirstName = ADMIN_FIRST_NAME,
                LastName = ADMIN_LAST_NAME,
                PhoneNumber = "+234 000 000 0000", // Placeholder
                DateOfBirth = new DateTime(1990, 1, 1),
                Gender = "Other",
                SendWelcomeEmail = false
            };

            var createdAdmin = await _userService.CreateUserAsync(createRequest);

            if (createdAdmin == null)
            {
                _logger.LogError("❌ CRITICAL: Failed to create admin user!");
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ CRITICAL: Admin user creation FAILED",
                    LogLevel.Error
                );
                return false;
            }

            // Set admin role in Supabase Auth metadata
            await SetAdminRoleAsync(createdAdmin.Id);

            // Verify email automatically for admin
            await _userService.VerifyUserEmailAsync(createdAdmin.Id);
            
            // Verify phone automatically for admin
            await _userService.VerifyUserPhoneAsync(createdAdmin.Id);

            // Upgrade to Platinum tier
            await _userService.UpdateMembershipTierAsync(createdAdmin.Id, MembershipTier.Platinum);

            // Give admin 100,000 loyalty points
            await _userService.UpdateLoyaltyPointsAsync(createdAdmin.Id, 100000);

            await MID_HelperFunctions.DebugMessageAsync(
                "═══════════════════════════════════════════",
                LogLevel.Info
            );
            await MID_HelperFunctions.DebugMessageAsync(
                "✓ ADMIN USER CREATED SUCCESSFULLY",
                LogLevel.Info
            );
            await MID_HelperFunctions.DebugMessageAsync(
                "═══════════════════════════════════════════",
                LogLevel.Info
            );

            _logger.LogWarning("═══════════════════════════════════════════");
            _logger.LogWarning("🔐 ADMIN USER CREATED - CREDENTIALS BELOW");
            _logger.LogWarning("═══════════════════════════════════════════");
            _logger.LogWarning("Email: {Email}", ADMIN_EMAIL);
            _logger.LogWarning("Password: {Password}", ADMIN_PASSWORD);
            _logger.LogWarning("User ID: {UserId}", createdAdmin.Id);
            _logger.LogWarning("Status: {Status}", createdAdmin.AccountStatus);
            _logger.LogWarning("Tier: {Tier}", createdAdmin.MembershipTier);
            _logger.LogWarning("Loyalty Points: {Points}", createdAdmin.LoyaltyPoints);
            _logger.LogWarning("═══════════════════════════════════════════");
            _logger.LogWarning("⚠️ STORE THESE CREDENTIALS SECURELY!");
            _logger.LogWarning("═══════════════════════════════════════════");

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Ensuring admin user exists");
            _logger.LogCritical(ex, "❌ CRITICAL ERROR: Failed to ensure admin user exists!");
            return false;
        }
    }

    public async Task<bool> IsAdminUserAsync(string email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            return email.Equals(ADMIN_EMAIL, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user is admin");
            return false;
        }
    }

    public async Task<AdminUserInfo?> GetAdminUserInfoAsync()
    {
        try
        {
            var admin = await _userService.GetUserByEmailAsync(ADMIN_EMAIL);
            
            if (admin == null)
                return null;

            return new AdminUserInfo
            {
                Email = admin.Email,
                UserId = admin.Id,
                FullName = admin.FullName,
                IsActive = admin.AccountStatus == "Active",
                CreatedAt = admin.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting admin user info");
            return null;
        }
    }

    /// <summary>
    /// Set admin role in user's Supabase Auth metadata
    /// </summary>
    private async Task<bool> SetAdminRoleAsync(string userId)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Setting admin role for user: {userId}",
                LogLevel.Info
            );

            // Update user metadata to include admin role
            var updated = await _supabaseClient.Auth.Update(new Supabase.Gotrue.UserAttributes
            {
                Data = new Dictionary<string, object>
                {
                    { "role", "admin" },
                    { "user_role", "admin" },
                    { "is_admin", true },
                    { "admin_level", "super" },
                    { "permissions", new[] { "all" } }
                }
            });

            if (updated != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✓ Admin role set successfully",
                    LogLevel.Info
                );
                return true;
            }

            _logger.LogWarning("Failed to set admin role - update returned null");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set admin role for user: {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Ensure existing admin user has proper role metadata
    /// </summary>
    private async Task<bool> EnsureAdminRoleAsync(string userId)
    {
        try
        {
            // Check if current user already has admin role
            var currentUser = await _supabaseClient.Auth.GetUser();
            
            if (currentUser?.User?.Id == userId)
            {
                var userData = currentUser.User.UserMetadata;
                
                if (userData != null && 
                    userData.ContainsKey("role") && 
                    userData["role"]?.ToString() == "admin")
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "✓ Admin role already set",
                        LogLevel.Info
                    );
                    return true;
                }
            }

            // Role not set or incorrect - update it
            await MID_HelperFunctions.DebugMessageAsync(
                "Updating admin role metadata...",
                LogLevel.Warning
            );
            
            return await SetAdminRoleAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not verify/update admin role for user: {UserId}", userId);
            return false;
        }
    }
}
