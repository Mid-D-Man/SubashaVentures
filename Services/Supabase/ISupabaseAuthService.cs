namespace SubashaVentures.Services.SupaBase;
using Microsoft.AspNetCore.Components.Authorization;
using Supabase.Gotrue;

public interface ISupabaseAuthService
{
    /// <summary>
    /// Authenticate user with email and password - DEPRECATED: Use Auth0 instead
    /// </summary>
    [Obsolete("Use Auth0 for authentication instead")]
    Task<bool> LoginAsync(string email, string password);
        
    /// <summary>
    /// Register new user account - DEPRECATED: Use Auth0 instead
    /// </summary>
    [Obsolete("Use Auth0 for authentication instead")]
    Task<bool> RegisterAsync(string email, string password, Dictionary<string, object>? userData = null);
        
    /// <summary>
    /// Sign out current user - DEPRECATED: Use Auth0 instead
    /// </summary>
    [Obsolete("Use Auth0 for authentication instead")]
    Task LogoutAsync();
        
    /// <summary>
    /// Get current authenticated user from Auth0 state
    /// </summary>
    Task<User?> GetCurrentUserAsync();
        
    /// <summary>
    /// Check if user is authenticated via Auth0
    /// </summary>
    Task<bool> IsAuthenticatedAsync();
        
    /// <summary>
    /// Reset user password - DEPRECATED: Use Auth0 instead
    /// </summary>
    [Obsolete("Use Auth0 for authentication instead")]
    Task<bool> ResetPasswordAsync(string email);
        
    /// <summary>
    /// Update user profile information - DEPRECATED: Use Auth0 instead
    /// </summary>
    [Obsolete("Use Auth0 for authentication instead")]
    Task<bool> UpdateUserProfileAsync(Dictionary<string, object> updates);
        
    /// <summary>
    /// Refresh current session - DEPRECATED: Use Auth0 instead
    /// </summary>
    [Obsolete("Use Auth0 for authentication instead")]
    Task<bool> RefreshSessionAsync();

    /// <summary>
    /// Configure Supabase client with Auth0 access token
    /// </summary>
    Task<bool> ConfigureSupabaseWithAuth0TokenAsync();

    /// <summary>
    /// Get user roles from Auth0 for Supabase RLS policies
    /// </summary>
    Task<List<string>> GetUserRolesAsync();

    /// <summary>
    /// Get Auth0 user ID for Supabase operations
    /// </summary>
    Task<string?> GetUserIdAsync();
}