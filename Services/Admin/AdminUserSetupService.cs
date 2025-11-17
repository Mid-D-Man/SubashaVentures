// Services/Admin/AdminUserSetupService.cs - NEW
using SubashaVentures.Services.Users;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Admin;

/// <summary>
/// Service for setting up the predefined admin user
/// </summary>
public interface IAdminUserSetupService
{
    Task<bool> EnsureAdminUserExistsAsync();
    Task<bool> IsAdminUserAsync(string email);
}

public class AdminUserSetupService : IAdminUserSetupService
{
    private readonly IUserService _userService;
    private readonly Supabase.Client _supabaseClient;
    private readonly ILogger<AdminUserSetupService> _logger;

    // PREDEFINED ADMIN CREDENTIALS
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

    /// <summary>
    /// Ensures the admin user exists in the system
    /// Call this on application startup
    /// </summary>
    public async Task<bool> EnsureAdminUserExistsAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Checking for admin user existence...",
                LogLevel.Info
            );

            // Check if admin user already exists
            var existingAdmin = await _userService.GetUserByEmailAsync(ADMIN_EMAIL);

            if (existingAdmin != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Admin user already exists: {ADMIN_EMAIL}",
                    LogLevel.Info
                );
                return true;
            }

            // Create admin user
            await MID_HelperFunctions.DebugMessageAsync(
                "Creating admin user...",
                LogLevel.Warning
            );

            var createRequest = new CreateUserRequest
            {
                Email = ADMIN_EMAIL,
                Password = ADMIN_PASSWORD,
                FirstName = ADMIN_FIRST_NAME,
                LastName = ADMIN_LAST_NAME,
                PhoneNumber = null,
                DateOfBirth = null,
                Gender = null,
                SendWelcomeEmail = false
            };

            var createdAdmin = await _userService.CreateUserAsync(createRequest);

            if (createdAdmin == null)
            {
                _logger.LogError("Failed to create admin user");
                return false;
            }

            // Set admin role in Supabase Auth metadata
            await SetAdminRoleAsync(createdAdmin.Id);

            // Verify email automatically for admin
            await _userService.VerifyUserEmailAsync(createdAdmin.Id);

            // Upgrade to Platinum tier
            await _userService.UpdateMembershipTierAsync(createdAdmin.Id, Domain.User.MembershipTier.Platinum);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Admin user created successfully: {ADMIN_EMAIL}",
                LogLevel.Info
            );

            _logger.LogInformation("═══════════════════════════════════════════");
            _logger.LogInformation("ADMIN USER CREATED");
            _logger.LogInformation("Email: {Email}", ADMIN_EMAIL);
            _logger.LogInformation("Password: {Password}", ADMIN_PASSWORD);
            _logger.LogInformation("═══════════════════════════════════════════");

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Ensuring admin user exists");
            _logger.LogError(ex, "Failed to ensure admin user exists");
            return false;
        }
    }

    /// <summary>
    /// Check if a user is the admin user
    /// </summary>
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

    /// <summary>
    /// Set admin role in user's metadata
    /// </summary>
    private async Task<bool> SetAdminRoleAsync(string userId)
    {
        try
        {
            // Update user metadata to include admin role
            var updated = await _supabaseClient.Auth.Update(new Supabase.Gotrue.UserAttributes
            {
                Data = new Dictionary<string, object>
                {
                    { "role", "admin" },
                    { "user_role", "admin" },
                    { "is_admin", true }
                }
            });

            return updated != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set admin role for user: {UserId}", userId);
            return false;
        }
    }
}
