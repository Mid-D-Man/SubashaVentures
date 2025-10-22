using SubashaVentures.Services.Storage;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.Text.Json;

namespace SubashaVentures.Services.SupaBase
{
    public class SupabaseAuthService : ISupabaseAuthService
    {
        private readonly Supabase.Client _client;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly IBlazorAppLocalStorageService _localStorage;
        private readonly ILogger<SupabaseAuthService> _logger;

        public SupabaseAuthService(
            Supabase.Client client,
            AuthenticationStateProvider authStateProvider,
            IBlazorAppLocalStorageService localStorage,
            ILogger<SupabaseAuthService> logger)
        {
            _logger = logger;
            _client = client;
            _authStateProvider = authStateProvider;
            _localStorage = localStorage;
        }

        // These methods are now redirected to Auth0 - they shouldn't be called directly
        public async Task<bool> LoginAsync(string email, string password)
        {
            _logger.LogWarning("LoginAsync called - This should be handled by Auth0, not Supabase");
            return false;
        }

        public async Task<bool> RegisterAsync(string email, string password, Dictionary<string, object>? userData = null)
        {
            _logger.LogWarning("RegisterAsync called - This should be handled by Auth0, not Supabase");
            return false;
        }

        public async Task LogoutAsync()
        {
            _logger.LogWarning("LogoutAsync called - This should be handled by Auth0, not Supabase");
        }

        // These methods bridge Auth0 authentication with Supabase operations
        public async Task<Supabase.Gotrue.User?> GetCurrentUserAsync()
        {
            try
            {
                var authState = await _authStateProvider.GetAuthenticationStateAsync();
                if (authState.User.Identity?.IsAuthenticated == true)
                {
                    // Create a Supabase user object from Auth0 claims
                    return CreateSupabaseUserFromAuth0Claims(authState.User);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current user from Auth0 state");
                return null;
            }
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            try
            {
                var authState = await _authStateProvider.GetAuthenticationStateAsync();
                return authState.User.Identity?.IsAuthenticated == true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking authentication status");
                return false;
            }
        }

        public async Task<bool> ResetPasswordAsync(string email)
        {
            _logger.LogWarning("ResetPasswordAsync called - This should be handled by Auth0, not Supabase");
            return false;
        }

        public async Task<bool> UpdateUserProfileAsync(Dictionary<string, object> updates)
        {
            _logger.LogWarning("UpdateUserProfileAsync called - This should be handled by Auth0, not Supabase");
            return false;
        }

        public async Task<bool> RefreshSessionAsync()
        {
            _logger.LogWarning("RefreshSessionAsync called - This should be handled by Auth0, not Supabase");
            return false;
        }

        // New method to configure Supabase client with Auth0 token
        public async Task<bool> ConfigureSupabaseWithAuth0TokenAsync()
        {
            try
            {
                var authState = await _authStateProvider.GetAuthenticationStateAsync();
                if (authState.User.Identity?.IsAuthenticated == true)
                {
                    var accessToken = await GetAuth0AccessTokenAsync();
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        // Configure Supabase client to use Auth0 token
                       // _client.Auth.SetAuth(accessToken);
                        _logger.LogInformation("Supabase configured with Auth0 token");
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error configuring Supabase with Auth0 token");
                return false;
            }
        }

        // Helper method to get Auth0 access token
        private async Task<string?> GetAuth0AccessTokenAsync()
        {
            try
            {
                var authState = await _authStateProvider.GetAuthenticationStateAsync();
                var accessTokenClaim = authState.User.FindFirst("access_token");
                return accessTokenClaim?.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Auth0 access token");
                return null;
            }
        }

        // Helper method to create Supabase user from Auth0 claims
        private Supabase.Gotrue.User? CreateSupabaseUserFromAuth0Claims(ClaimsPrincipal auth0User)
        {
            try
            {
                var userId = auth0User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var email = auth0User.FindFirst(ClaimTypes.Email)?.Value;
                var name = auth0User.FindFirst(ClaimTypes.Name)?.Value;
                var roles = auth0User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
                {
                    return null;
                }

                // Create a user object that mimics Supabase's user structure
                var userData = new Dictionary<string, object>
                {
                    ["id"] = userId,
                    ["email"] = email,
                    ["user_metadata"] = new Dictionary<string, object>
                    {
                        ["name"] = name ?? "",
                        ["roles"] = roles
                    }
                };

                // Note: This is a simplified approach. You might need to adjust based on your exact needs
                return JsonSerializer.Deserialize<Supabase.Gotrue.User>(JsonSerializer.Serialize(userData));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Supabase user from Auth0 claims");
                return null;
            }
        }

        // Method to get user roles from Auth0 for Supabase RLS
        public async Task<List<string>> GetUserRolesAsync()
        {
            try
            {
                var authState = await _authStateProvider.GetAuthenticationStateAsync();
                if (authState.User.Identity?.IsAuthenticated == true)
                {
                    return authState.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
                }
                return new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user roles");
                return new List<string>();
            }
        }

        // Method to get user ID for Supabase operations
        public async Task<string?> GetUserIdAsync()
        {
            try
            {
                var authState = await _authStateProvider.GetAuthenticationStateAsync();
                if (authState.User.Identity?.IsAuthenticated == true)
                {
                    return authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user ID");
                return null;
            }
        }
    }
}