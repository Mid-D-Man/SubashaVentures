using SubashaVentures.Services.Storage;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;

namespace SubashaVentures.Services.Supabase;

public class SupabaseAuthService : ISupabaseAuthService
{
    private readonly Client _client;
    private readonly IBlazorAppLocalStorageService _localStorage;
    private readonly ILogger<SupabaseAuthService> _logger;
    
    private const string SESSION_STORAGE_KEY = "supabase_session";
    private const string USER_STORAGE_KEY = "supabase_user";

    public SupabaseAuthService(
       Client client,
        IBlazorAppLocalStorageService localStorage,
        ILogger<SupabaseAuthService> logger)
    {
        _client = client;
        _localStorage = localStorage;
        _logger = logger;
    }

    public async Task<SupabaseAuthResult> SignInAsync(string email, string password)
    {
        try
        {
            if (!MID_HelperFunctions.IsValidString(email) || !MID_HelperFunctions.IsValidString(password))
            {
                return new SupabaseAuthResult
                {
                    Success = false,
                    Message = "Email and password are required",
                    ErrorCode = "INVALID_CREDENTIALS"
                };
            }

            var session = await _client.SignIn(email, password);
            
            if (session?.User == null)
            {
                return new SupabaseAuthResult
                {
                    Success = false,
                    Message = "Authentication failed",
                    ErrorCode = "AUTH_FAILED"
                };
            }

            // Store session info
            await StoreSessionAsync(session);

            _logger.LogInformation("User signed in successfully: {Email}", email);

            return new SupabaseAuthResult
            {
                Success = true,
                Message = "Sign in successful",
                Session = MapToSessionInfo(session),
                User = await GetUserProfileAsync(session.User.Id)
            };
        }
        catch (GotrueException ex)
        {
            _logger.LogError(ex, "Supabase authentication error: {Message}", ex.Message);
            return new SupabaseAuthResult
            {
                Success = false,
                Message = ex.Message,
                ErrorCode = ex.Message.Contains("Invalid") ? "INVALID_CREDENTIALS" : "AUTH_ERROR"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during sign in");
            return new SupabaseAuthResult
            {
                Success = false,
                Message = "An unexpected error occurred",
                ErrorCode = "UNEXPECTED_ERROR"
            };
        }
    }

    public async Task<SupabaseAuthResult> SignUpAsync(string email, string password, UserModel userData)
    {
        try
        {
            if (!MID_HelperFunctions.IsValidString(email) || !MID_HelperFunctions.IsValidString(password))
            {
                return new SupabaseAuthResult
                {
                    Success = false,
                    Message = "Email and password are required",
                    ErrorCode = "INVALID_INPUT"
                };
            }

            // Create user metadata
            var userMetadata = new Dictionary<string, object>
            {
                { "first_name", userData.FirstName },
                { "last_name", userData.LastName },
                { "phone_number", userData.PhoneNumber ?? string.Empty },
                { "avatar_url", userData.AvatarUrl ?? string.Empty }
            };

            var session = await _client.SignUp(email, password, new SignUpOptions
            {
                Data = userMetadata
            });

            if (session?.User == null)
            {
                return new SupabaseAuthResult
                {
                    Success = false,
                    Message = "Registration failed",
                    ErrorCode = "SIGNUP_FAILED"
                };
            }

            // Store session info
            await StoreSessionAsync(session);

            _logger.LogInformation("User signed up successfully: {Email}", email);

            // Create new UserModel instead of using 'with' syntax
            var newUserData = new UserModel
            {
                Id = session.User.Id,
                Email = email,
                FirstName = userData.FirstName,
                LastName = userData.LastName,
                PhoneNumber = userData.PhoneNumber,
                AvatarUrl = userData.AvatarUrl,
                DateOfBirth = userData.DateOfBirth,
                Gender = userData.Gender,
                IsEmailVerified = userData.IsEmailVerified,
                IsPhoneVerified = userData.IsPhoneVerified,
                AccountStatus = userData.AccountStatus,
                EmailNotifications = userData.EmailNotifications,
                SmsNotifications = userData.SmsNotifications,
                PreferredLanguage = userData.PreferredLanguage,
                Currency = userData.Currency,
                TotalOrders = userData.TotalOrders,
                TotalSpent = userData.TotalSpent,
                LoyaltyPoints = userData.LoyaltyPoints,
                MembershipTier = userData.MembershipTier,
                CreatedAt = userData.CreatedAt,
                CreatedBy = userData.CreatedBy,
                UpdatedAt = userData.UpdatedAt,
                UpdatedBy = userData.UpdatedBy,
                IsDeleted = userData.IsDeleted,
                DeletedAt = userData.DeletedAt,
                DeletedBy = userData.DeletedBy,
                LastLoginAt = userData.LastLoginAt
            };

            return new SupabaseAuthResult
            {
                Success = true,
                Message = "Registration successful. Please check your email to verify your account.",
                Session = MapToSessionInfo(session),
                User = newUserData
            };
        }
        catch (GotrueException ex)
        {
            _logger.LogError(ex, "Supabase signup error: {Message}", ex.Message);
            return new SupabaseAuthResult
            {
                Success = false,
                Message = ex.Message,
                ErrorCode = ex.Message.Contains("already registered") ? "USER_EXISTS" : "SIGNUP_ERROR"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during sign up");
            return new SupabaseAuthResult
            {
                Success = false,
                Message = "An unexpected error occurred",
                ErrorCode = "UNEXPECTED_ERROR"
            };
        }
    }

    public async Task<bool> SignOutAsync()
    {
        try
        {
            await _client.SignOut();
            
            // Clear stored session
            await _localStorage.RemoveItemAsync(SESSION_STORAGE_KEY);
            await _localStorage.RemoveItemAsync(USER_STORAGE_KEY);
            
            _logger.LogInformation("User signed out successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing out");
            return false;
        }
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        try
        {
            return _client.CurrentUser;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user");
            return null;
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            var user = await GetCurrentUserAsync();
            return user != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking authentication status");
            return false;
        }
    }

    public async Task<bool> RefreshSessionAsync()
    {
        try
        {
            var session = await _client.RefreshSession();
            
            if (session != null)
            {
                await StoreSessionAsync(session);
                _logger.LogInformation("Session refreshed successfully");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing session");
            return false;
        }
    }

    public async Task<bool> SendPasswordResetEmailAsync(string email)
    {
        try
        {
            if (!MID_HelperFunctions.IsValidString(email))
                return false;

            await _client.ResetPasswordForEmail(email);
            _logger.LogInformation("Password reset email sent to: {Email}", email);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending password reset email");
            return false;
        }
    }

    public async Task<bool> UpdatePasswordAsync(string newPassword)
    {
        try
        {
            if (!MID_HelperFunctions.IsValidString(newPassword))
                return false;

            var user = await _client.Update(new UserAttributes
            {
                Password = newPassword
            });

            return user != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating password");
            return false;
        }
    }

    public async Task<bool> UpdateUserProfileAsync(Dictionary<string, object> updates)
    {
        try
        {
            if (updates == null || updates.Count == 0)
                return false;

            var user = await _client.Update(new UserAttributes
            {
                Data = updates
            });

            return user != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user profile");
            return false;
        }
    }

    public async Task<SupabaseSessionInfo?> GetSessionInfoAsync()
    {
        try
        {
            var session = _client.CurrentSession;
            return session != null ? MapToSessionInfo(session) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving session info");
            return null;
        }
    }

    public async Task<bool> VerifyEmailAsync(string token)
    {
        try
        {
            if (!MID_HelperFunctions.IsValidString(token))
                return false;

            // Supabase handles email verification automatically via magic links
            // This method is a placeholder for custom verification logic if needed
            _logger.LogInformation("Email verification requested with token");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying email");
            return false;
        }
    }

    private async Task StoreSessionAsync(Session session)
    {
        try
        {
            var sessionInfo = MapToSessionInfo(session);
            var sessionJson = JsonHelper.Serialize(sessionInfo);
            await _localStorage.SetItemAsync(SESSION_STORAGE_KEY, sessionJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store session in local storage");
        }
    }

    private SupabaseSessionInfo MapToSessionInfo(Session session)
    {
        long expiration = session.ExpiresIn;
        return new SupabaseSessionInfo
        {
            AccessToken = session.AccessToken ?? string.Empty,
            RefreshToken = session.RefreshToken ?? string.Empty,
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(expiration).UtcDateTime,
            UserId = session.User?.Id ?? string.Empty,
            UserEmail = session.User?.Email ?? string.Empty
        };
    }

    private async Task<UserModel?> GetUserProfileAsync(string userId)
    {
        try
        {
            // Fetch user profile from database
            // This is a placeholder - implement actual database query
            var storedUser = await _localStorage.GetItemAsync<UserModel>(USER_STORAGE_KEY);
            return storedUser;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve user profile");
            return null;
        }
    }
}
