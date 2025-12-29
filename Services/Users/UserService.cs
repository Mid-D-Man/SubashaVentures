// Services/Users/UserService.cs - FIXED GetUser JWT parameter
using SubashaVentures.Models.Supabase;
using SubashaVentures.Domain.User;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Gotrue;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;
using Client = Supabase.Client;

namespace SubashaVentures.Services.Users;

public interface IUserService
{
    Task<UserModel?> GetUserByIdAsync(string userId);
    Task<UserModel?> GetUserByEmailAsync(string email);
    Task<bool> UpdateUserAsync(UserModel user);
    Task<bool> EnsureUserProfileExistsAsync(string userId);
    Task<List<UserModel>> GetAllUsersAsync();
    Task<bool> UpdateUserStatusAsync(string userId, string status, string? reason = null);
}

public class UserService : IUserService
{
    private readonly Client _supabase;
    private readonly ILogger<UserService> _logger;

    public UserService(
        Client supabase,
        ILogger<UserService> logger)
    {
        _supabase = supabase;
        _logger = logger;
    }

    public async Task<UserModel?> GetUserByIdAsync(string userId)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Getting user by ID: {userId}",
                LogLevel.Info
            );

            var result = await _supabase
                .From<UserModel>()
                .Where(u => u.Id == userId)
                .Single();

            if (result != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ User found: {result.Email}",
                    LogLevel.Info
                );
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"⚠️ User not found with ID: {userId}",
                    LogLevel.Warning
                );
            }

            return result;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting user by ID: {userId}");
            _logger.LogError(ex, "Error getting user by ID: {UserId}", userId);
            return null;
        }
    }

    public async Task<UserModel?> GetUserByEmailAsync(string email)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Getting user by email: {email}",
                LogLevel.Info
            );

            var result = await _supabase
                .From<UserModel>()
                .Where(u => u.Email == email)
                .Single();

            if (result != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ User found: {result.Email}",
                    LogLevel.Info
                );
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"⚠️ User not found with email: {email}",
                    LogLevel.Warning
                );
            }

            return result;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting user by email: {email}");
            _logger.LogError(ex, "Error getting user by email: {Email}", email);
            return null;
        }
    }

    public async Task<bool> UpdateUserAsync(UserModel user)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Updating user: {user.Email}",
                LogLevel.Info
            );

            user.UpdatedAt = DateTime.UtcNow;

            await _supabase
                .From<UserModel>()
                .Where(u => u.Id == user.Id)
                .Update(user);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ User updated successfully: {user.Email}",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Updating user: {user.Email}");
            _logger.LogError(ex, "Error updating user: {UserId}", user.Id);
            return false;
        }
    }

    public async Task<bool> EnsureUserProfileExistsAsync(string userId)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"🔍 Ensuring user profile exists for: {userId}",
                LogLevel.Info
            );

            // Check if profile already exists
            var existingProfile = await _supabase
                .From<UserModel>()
                .Where(u => u.Id == userId)
                .Single();

            if (existingProfile != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ User profile already exists for: {userId}",
                    LogLevel.Info
                );
                return true;
            }

            // ✅ CRITICAL FIX: Get current session and pass JWT to GetUser
            var session = _supabase.Auth.CurrentSession;
            
            if (session == null || string.IsNullOrEmpty(session.AccessToken))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ No active session found, cannot create user profile",
                    LogLevel.Error
                );
                return false;
            }

            // Get auth user with JWT token
            var authUser = await _supabase.Auth.GetUser(session.AccessToken);

            if (authUser == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"❌ Auth user not found for ID: {userId}",
                    LogLevel.Error
                );
                return false;
            }

            // Create new user profile
            var userProfile = new UserModel
            {
                Id = authUser.Id,
                Email = authUser.Email ?? "",
                FirstName = authUser.UserMetadata?.GetValueOrDefault("first_name")?.ToString() ?? "",
                LastName = authUser.UserMetadata?.GetValueOrDefault("last_name")?.ToString() ?? "",
                AvatarUrl = authUser.UserMetadata?.GetValueOrDefault("avatar_url")?.ToString(),
                IsEmailVerified = authUser.EmailConfirmedAt != null,
                IsPhoneVerified = false,
                AccountStatus = "Active",
                EmailNotifications = true,
                SmsNotifications = false,
                PreferredLanguage = "en",
                Currency = "NGN",
                MembershipTier = "Bronze",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = authUser.Id
            };

            await _supabase.From<UserModel>().Insert(userProfile);

            // Create default user role
            var userRole = new UserRoleModel
            {
                Id = Guid.NewGuid().ToString(),
                UserId = authUser.Id,
                Role = "user",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = authUser.Id
            };

            await _supabase.From<UserRoleModel>().Insert(userRole);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ User profile created successfully for: {authUser.Email}",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Ensuring user profile exists: {userId}");
            _logger.LogError(ex, "Error ensuring user profile exists: {UserId}", userId);
            return false;
        }
    }

    public async Task<List<UserModel>> GetAllUsersAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Getting all users",
                LogLevel.Info
            );

            var result = await _supabase
                .From<UserModel>()
                .Get();

            var users = result?.Models ?? new List<UserModel>();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Retrieved {users.Count} users",
                LogLevel.Info
            );

            return users;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting all users");
            _logger.LogError(ex, "Error getting all users");
            return new List<UserModel>();
        }
    }

    public async Task<bool> UpdateUserStatusAsync(string userId, string status, string? reason = null)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Updating user status: {userId} to {status}",
                LogLevel.Info
            );

            var user = await GetUserByIdAsync(userId);

            if (user == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"❌ User not found: {userId}",
                    LogLevel.Warning
                );
                return false;
            }

            user.AccountStatus = status;
            user.SuspensionReason = reason;
            user.UpdatedAt = DateTime.UtcNow;

            await _supabase
                .From<UserModel>()
                .Where(u => u.Id == userId)
                .Update(user);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ User status updated: {userId} -> {status}",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Updating user status: {userId}");
            _logger.LogError(ex, "Error updating user status: {UserId}", userId);
            return false;
        }
    }
}
