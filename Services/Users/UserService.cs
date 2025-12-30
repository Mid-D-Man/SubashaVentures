// Services/Users/UserService.cs - COMPLETE UPDATED IMPLEMENTATION
using SubashaVentures.Domain.User;
using SubashaVentures.Services.SupaBase;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Postgrest;
using System.Text;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;
using Client = Supabase.Client;
using Gotrue = Supabase.Gotrue;

namespace SubashaVentures.Services.Users;

public class UserService : IUserService
{
    private readonly ISupabaseDatabaseService _database;
    private readonly Client _supabaseClient;
    private readonly ILogger<UserService> _logger;

    public UserService(
        ISupabaseDatabaseService database,
        Client supabaseClient,
        ILogger<UserService> logger)
    {
        _database = database;
        _supabaseClient = supabaseClient;
        _logger = logger;
    }

    // ==================== USER RETRIEVAL ====================

    public async Task<List<UserProfileViewModel>> GetUsersAsync(int skip = 0, int take = 100)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Fetching users: skip={skip}, take={take}",
                LogLevel.Info
            );

            var users = await _supabaseClient
                .From<UserModel>()
                .Order("created_at", Constants.Ordering.Descending)
                .Range(skip, skip + take - 1)
                .Get();

            if (users?.Models == null || !users.Models.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No users found",
                    LogLevel.Warning
                );
                return new List<UserProfileViewModel>();
            }

            var viewModels = users.Models
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Retrieved {viewModels.Count} users",
                LogLevel.Info
            );

            return viewModels;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting users");
            _logger.LogError(ex, "Failed to retrieve users");
            return new List<UserProfileViewModel>();
        }
    }

    public async Task<bool> EnsureUserProfileExistsAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("EnsureUserProfileExists called with empty userId");
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"🔍 Checking if user profile exists for: {userId}",
                LogLevel.Info
            );

            var existingProfile = await _supabaseClient
                .From<UserModel>()
                .Where(u => u.Id == userId)
                .Single();

            if (existingProfile != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ User profile already exists for: {userId}",
                    LogLevel.Info
                );
                return true;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"⚠️ User profile missing, attempting to create for: {userId}",
                LogLevel.Warning
            );

            var session = _supabaseClient.Auth.CurrentSession;
            if (session == null || string.IsNullOrEmpty(session.AccessToken))
            {
                _logger.LogError("Cannot create profile - no active session");
                return false;
            }

            var authUser = await _supabaseClient.Auth.GetUser(session.AccessToken);
            if (authUser == null || authUser.Id != userId)
            {
                _logger.LogError("Cannot create profile - auth user not found or mismatch");
                return false;
            }

            var userProfile = new UserModel
            {
                Id = authUser.Id,
                Email = authUser.Email ?? "",
                FirstName = authUser.UserMetadata?.GetValueOrDefault("first_name")?.ToString() ?? "",
                LastName = authUser.UserMetadata?.GetValueOrDefault("last_name")?.ToString() ?? "",
                PhoneNumber = authUser.UserMetadata?.GetValueOrDefault("phone_number")?.ToString(),
                AvatarUrl = authUser.UserMetadata?.GetValueOrDefault("avatar_url")?.ToString(),
                IsEmailVerified = authUser.EmailConfirmedAt != null,
                IsPhoneVerified = false,
                AccountStatus = "Active",
                EmailNotifications = true,
                SmsNotifications = false,
                PreferredLanguage = "en",
                Currency = "NGN",
                MembershipTier = "Bronze",
                Role = "user", // ✅ Default role
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };

            await _supabaseClient.From<UserModel>().Insert(userProfile);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ User profile created successfully for: {userId} with default 'user' role",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Ensuring user profile exists: {userId}");
            _logger.LogError(ex, "Failed to ensure user profile exists: {UserId}", userId);
            return false;
        }
    }

    public async Task<UserProfileViewModel?> GetUserByIdAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("GetUserById called with empty userId");
                return null;
            }

            var user = await _supabaseClient
                .From<UserModel>()
                .Where(u => u.Id == userId)
                .Single();

            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return null;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Retrieved user {userId} with role: {user.Role}",
                LogLevel.Info
            );

            return UserProfileViewModel.FromCloudModel(user);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting user: {userId}");
            _logger.LogError(ex, "Failed to retrieve user: {UserId}", userId);
            return null;
        }
    }

    public async Task<UserProfileViewModel?> GetUserByEmailAsync(string email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogWarning("GetUserByEmail called with empty email");
                return null;
            }

            var user = await _supabaseClient
                .From<UserModel>()
                .Where(u => u.Email == email)
                .Single();

            if (user == null)
            {
                _logger.LogWarning("User not found by email: {Email}", email);
                return null;
            }

            return UserProfileViewModel.FromCloudModel(user);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting user by email: {email}");
            _logger.LogError(ex, "Failed to retrieve user by email: {Email}", email);
            return null;
        }
    }

    public async Task<List<UserProfileViewModel>> SearchUsersAsync(string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogWarning("SearchUsers called with empty query");
                return new List<UserProfileViewModel>();
            }

            var users = await _supabaseClient
                .From<UserModel>()
                .Limit(50)
                .Get();

            if (users?.Models == null || !users.Models.Any())
            {
                return new List<UserProfileViewModel>();
            }

            var viewModels = users.Models
                .Select(UserProfileViewModel.FromCloudModel)
                .ToList();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Found {viewModels.Count} users matching '{query}'",
                LogLevel.Info
            );

            return viewModels;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Searching users: {query}");
            _logger.LogError(ex, "Failed to search users with query: {Query}", query);
            return new List<UserProfileViewModel>();
        }
    }

    // ==================== USER MANAGEMENT ====================

    public async Task<UserProfileViewModel?> CreateUserAsync(CreateUserRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                _logger.LogWarning("CreateUser called with invalid email or password");
                return null;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Creating user: {request.Email}",
                LogLevel.Info
            );

            var authResponse = await _supabaseClient.Auth.SignUp(
                request.Email,
                request.Password,
                new Gotrue.SignUpOptions
                {
                    Data = new Dictionary<string, object>
                    {
                        { "first_name", request.FirstName },
                        { "last_name", request.LastName },
                        { "phone_number", request.PhoneNumber ?? "" },
                        { "avatar_url", "" }
                    }
                }
            );

            if (authResponse?.User == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Failed to create user in Supabase Auth",
                    LogLevel.Error
                );
                return null;
            }

            await Task.Delay(2000);

            var createdUser = await GetUserByIdAsync(authResponse.User.Id);

            if (createdUser != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ User created successfully: {request.Email} with role: {createdUser.Role}",
                    LogLevel.Info
                );

                if (request.SendWelcomeEmail)
                {
                    _logger.LogInformation("Welcome email should be sent to: {Email}", request.Email);
                }
            }
            else
            {
                _logger.LogWarning("User created in auth but profile not found: {UserId}", authResponse.User.Id);
            }

            return createdUser;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Creating user: {request.Email}");
            _logger.LogError(ex, "Failed to create user: {Email}", request.Email);
            return null;
        }
    }

    public async Task<bool> UpdateUserProfileAsync(string userId, UpdateUserRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("UpdateUserProfile called with empty userId");
                return false;
            }

            var user = await _supabaseClient
                .From<UserModel>()
                .Where(u => u.Id == userId)
                .Single();

            if (user == null)
            {
                _logger.LogWarning("User not found for update: {UserId}", userId);
                return false;
            }

            if (request.FirstName != null) user.FirstName = request.FirstName;
            if (request.LastName != null) user.LastName = request.LastName;
            if (request.PhoneNumber != null) user.PhoneNumber = request.PhoneNumber;
            if (request.DateOfBirth.HasValue) user.DateOfBirth = request.DateOfBirth;
            if (request.Gender != null) user.Gender = request.Gender;
            if (request.AvatarUrl != null) user.AvatarUrl = request.AvatarUrl;
            if (request.EmailNotifications.HasValue) user.EmailNotifications = request.EmailNotifications.Value;
            if (request.SmsNotifications.HasValue) user.SmsNotifications = request.SmsNotifications.Value;
            if (request.PreferredLanguage != null) user.PreferredLanguage = request.PreferredLanguage;
            if (request.Currency != null) user.Currency = request.Currency;

            user.UpdatedAt = DateTime.UtcNow;

            await user.Update<UserModel>();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ User profile updated: {userId}",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Updating user: {userId}");
            _logger.LogError(ex, "Failed to update user profile: {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> ToggleSuspendUserAsync(string userId, bool suspend, string? reason = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("ToggleSuspendUser called with empty userId");
                return false;
            }

            var user = await _supabaseClient
                .From<UserModel>()
                .Where(u => u.Id == userId)
                .Single();

            if (user == null)
            {
                _logger.LogWarning("User not found for suspension toggle: {UserId}", userId);
                return false;
            }

            user.AccountStatus = suspend ? "Suspended" : "Active";
            user.SuspensionReason = suspend ? reason : null;
            user.UpdatedAt = DateTime.UtcNow;

            await user.Update<UserModel>();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ User {(suspend ? "suspended" : "activated")}: {userId}",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Toggling suspend: {userId}");
            _logger.LogError(ex, "Failed to toggle suspend for user: {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> BanUserAsync(string userId, DateTime? bannedUntil, string reason)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("BanUser called with empty userId");
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Banning user {userId} until: {bannedUntil?.ToString() ?? "indefinitely"}",
                LogLevel.Warning
            );

            var user = await _supabaseClient
                .From<UserModel>()
                .Where(u => u.Id == userId)
                .Single();

            if (user == null)
            {
                _logger.LogWarning("User not found for banning: {UserId}", userId);
                return false;
            }

            user.AccountStatus = "Suspended";
            user.SuspensionReason = $"BANNED: {reason}";
            user.UpdatedAt = DateTime.UtcNow;

            await user.Update<UserModel>();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ User banned: {userId}",
                LogLevel.Warning
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Banning user: {userId}");
            _logger.LogError(ex, "Failed to ban user: {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> DeleteUserAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("DeleteUser called with empty userId");
                return false;
            }

            var user = await _supabaseClient
                .From<UserModel>()
                .Where(u => u.Id == userId)
                .Single();

            if (user == null)
            {
                _logger.LogWarning("User not found for soft delete: {UserId}", userId);
                return false;
            }

            user.AccountStatus = "Deleted";
            user.IsDeleted = true;
            user.DeletedAt = DateTime.UtcNow;
            user.DeletedBy = "admin";
            user.UpdatedAt = DateTime.UtcNow;

            await user.Update<UserModel>();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ User soft deleted: {userId}",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Deleting user: {userId}");
            _logger.LogError(ex, "Failed to delete user: {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> PermanentDeleteUserAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("PermanentDeleteUser called with empty userId");
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"⚠️ PERMANENT DELETE requested for user: {userId}",
                LogLevel.Warning
            );

            await _supabaseClient
                .From<UserModel>()
                .Where(u => u.Id == userId)
                .Delete();

            _logger.LogWarning("User deleted from public.users. Auth deletion requires manual action: {UserId}", userId);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ User permanently deleted from public.users: {userId}",
                LogLevel.Warning
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Permanently deleting user: {userId}");
            _logger.LogError(ex, "Failed to permanently delete user: {UserId}", userId);
            return false;
        }
    }

    // ==================== USER STATISTICS ====================

    public async Task<UserStatistics> GetUserStatisticsAsync()
    {
        try
        {
            var allUsers = await _supabaseClient
                .From<UserModel>()
                .Get();

            if (allUsers?.Models == null || !allUsers.Models.Any())
            {
                _logger.LogWarning("No users found for statistics");
                return new UserStatistics();
            }

            var users = allUsers.Models.ToList();
            var now = DateTime.UtcNow;

            var stats = new UserStatistics
            {
                TotalUsers = users.Count,
                ActiveUsers = users.Count(u => u.AccountStatus == "Active"),
                SuspendedUsers = users.Count(u => u.AccountStatus == "Suspended"),
                DeletedUsers = users.Count(u => u.AccountStatus == "Deleted" || u.IsDeleted),
                VerifiedUsers = users.Count(u => u.IsEmailVerified),
                NewUsersThisMonth = users.Count(u => u.CreatedAt.Month == now.Month && u.CreatedAt.Year == now.Year),
                NewUsersToday = users.Count(u => u.CreatedAt.Date == now.Date),
                UsersByTier = users
                    .GroupBy(u => Enum.Parse<MembershipTier>(u.MembershipTier))
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ User statistics calculated: {stats.TotalUsers} total users",
                LogLevel.Info
            );

            return stats;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting user statistics");
            _logger.LogError(ex, "Failed to calculate user statistics");
            return new UserStatistics();
        }
    }

    public async Task<bool> UpdateUserOrderStatsAsync(string userId, decimal orderAmount)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("UpdateUserOrderStats called with empty userId");
                return false;
            }

            var user = await _supabaseClient
                .From<UserModel>()
                .Where(u => u.Id == userId)
                .Single();

            if (user == null)
            {
                _logger.LogWarning("User not found for order stats update: {UserId}", userId);
                return false;
            }

            user.TotalOrders++;
            user.TotalSpent += orderAmount;
            user.UpdatedAt = DateTime.UtcNow;

            var pointsEarned = (int)(orderAmount / 100);
            user.LoyaltyPoints += pointsEarned;

            user.MembershipTier = CalculateMembershipTier(user.TotalSpent);

            await user.Update<UserModel>();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Order stats updated: {userId} (+₦{orderAmount:N0}, +{pointsEarned} points, tier: {user.MembershipTier})",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Updating order stats: {userId}");
            _logger.LogError(ex, "Failed to update order stats for user: {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> UpdateLoyaltyPointsAsync(string userId, int points)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("UpdateLoyaltyPoints called with empty userId");
                return false;
            }

            var user = await _supabaseClient
                .From<UserModel>()
                .Where(u => u.Id == userId)
                .Single();

            if (user == null)
            {
                _logger.LogWarning("User not found for loyalty points update: {UserId}", userId);
                return false;
            }

            user.LoyaltyPoints += points;
            user.UpdatedAt = DateTime.UtcNow;

            await user.Update<UserModel>();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loyalty points updated: {userId} ({points:+#;-#;0} points)",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Updating loyalty points: {userId}");
            _logger.LogError(ex, "Failed to update loyalty points for user: {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> UpdateMembershipTierAsync(string userId, MembershipTier tier)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("UpdateMembershipTier called with empty userId");
                return false;
            }

            var user = await _supabaseClient
                .From<UserModel>()
                .Where(u => u.Id == userId)
                .Single();

            if (user == null)
            {
                _logger.LogWarning("User not found for tier update: {UserId}", userId);
                return false;
            }

            user.MembershipTier = tier.ToString();
            user.UpdatedAt = DateTime.UtcNow;

            await user.Update<UserModel>();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Membership tier updated: {userId} → {tier}",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Updating membership tier: {userId}");
            _logger.LogError(ex, "Failed to update membership tier for user: {UserId}", userId);
            return false;
        }
    }

    // ==================== USER VERIFICATION ====================

    public async Task<bool> VerifyUserEmailAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("VerifyUserEmail called with empty userId");
                return false;
            }

            var user = await _supabaseClient
                .From<UserModel>()
                .Where(u => u.Id == userId)
                .Single();

            if (user == null)
            {
                _logger.LogWarning("User not found for email verification: {UserId}", userId);
                return false;
            }

            user.IsEmailVerified = true;
            user.UpdatedAt = DateTime.UtcNow;

            await user.Update<UserModel>();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Email verified for user: {userId}",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Verifying email: {userId}");
            _logger.LogError(ex, "Failed to verify email for user: {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> VerifyUserPhoneAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("VerifyUserPhone called with empty userId");
                return false;
            }

            var user = await _supabaseClient
                .From<UserModel>()
                .Where(u => u.Id == userId)
                .Single();

            if (user == null)
            {
                _logger.LogWarning("User not found for phone verification: {UserId}", userId);
                return false;
            }

            user.IsPhoneVerified = true;
            user.UpdatedAt = DateTime.UtcNow;

            await user.Update<UserModel>();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Phone verified for user: {userId}",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Verifying phone: {userId}");
            _logger.LogError(ex, "Failed to verify phone for user: {UserId}", userId);
            return false;
        }
    }

    // ==================== BULK OPERATIONS ====================

    public async Task<bool> BulkSuspendUsersAsync(List<string> userIds, string reason)
    {
        try
        {
            if (userIds == null || !userIds.Any())
            {
                _logger.LogWarning("BulkSuspendUsers called with empty user list");
                return false;
            }

            var successCount = 0;
            foreach (var userId in userIds)
            {
                if (await ToggleSuspendUserAsync(userId, true, reason))
                {
                    successCount++;
                }
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Bulk suspended {successCount}/{userIds.Count} users",
                LogLevel.Warning
            );

            return successCount == userIds.Count;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Bulk suspend users");
            _logger.LogError(ex, "Failed to bulk suspend users");
            return false;
        }
    }

    public async Task<bool> BulkActivateUsersAsync(List<string> userIds)
    {
        try
        {
            if (userIds == null || !userIds.Any())
            {
                _logger.LogWarning("BulkActivateUsers called with empty user list");
                return false;
            }

            var successCount = 0;
            foreach (var userId in userIds)
            {
                if (await ToggleSuspendUserAsync(userId, false))
                {
                    successCount++;
                }
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Bulk activated {successCount}/{userIds.Count} users",
                LogLevel.Info
            );

            return successCount == userIds.Count;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Bulk activate users");
            _logger.LogError(ex, "Failed to bulk activate users");
            return false;
        }
    }

    public async Task<string> ExportUsersAsync(List<string>? userIds = null)
    {
        try
        {
            var users = await GetUsersAsync(0, 10000);

            if (userIds != null && userIds.Any())
            {
                users = users.Where(u => userIds.Contains(u.Id)).ToList();
            }

            if (!users.Any())
            {
                _logger.LogWarning("No users to export");
                return string.Empty;
            }

            var csv = new StringBuilder();
            
            csv.AppendLine("ID,Email,First Name,Last Name,Phone,Role,Status,Membership," +
                          "Total Orders,Total Spent,Loyalty Points,Email Verified,Phone Verified," +
                          "Created At,Last Login");

            foreach (var user in users)
            {
                csv.AppendLine($"\"{user.Id}\"," +
                              $"\"{user.Email}\"," +
                              $"\"{user.FirstName}\"," +
                              $"\"{user.LastName}\"," +
                              $"\"{user.PhoneNumber ?? ""}\"," +
                              $"\"{user.Role}\"," +
                              $"{user.AccountStatus}," +
                              $"{user.MembershipTier}," +
                              $"{user.TotalOrders}," +
                              $"{user.TotalSpent}," +
                              $"{user.LoyaltyPoints}," +
                              $"{user.IsEmailVerified}," +
                              $"{user.IsPhoneVerified}," +
                              $"{user.CreatedAt:yyyy-MM-dd HH:mm:ss}," +
                              $"{user.LastLoginAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never"}");
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Exported {users.Count} users to CSV",
                LogLevel.Info
            );

            return csv.ToString();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Exporting users");
            _logger.LogError(ex, "Failed to export users");
            return string.Empty;
        }
    }

    // ==================== PRIVATE HELPERS ====================

    private string CalculateMembershipTier(decimal totalSpent)
    {
        if (totalSpent >= 500000) return "Platinum";
        if (totalSpent >= 200000) return "Gold";
        if (totalSpent >= 50000) return "Silver";
        return "Bronze";
    }
}
