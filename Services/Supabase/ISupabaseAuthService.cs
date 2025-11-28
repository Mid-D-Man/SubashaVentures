// Services/Supabase/ISupabaseAuthService.cs - UPDATED with OAuth
using SubashaVentures.Models.Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace SubashaVentures.Services.Supabase;

/// <summary>
/// Service for Supabase authentication operations with OAuth support
/// </summary>
public interface ISupabaseAuthService
{
    /// <summary>
    /// Sign in with email and password
    /// </summary>
    Task<SupabaseAuthResult> SignInAsync(string email, string password);
    
    /// <summary>
    /// Sign up with email and password
    /// </summary>
    Task<SupabaseAuthResult> SignUpAsync(string email, string password, UserModel userData);
    
    /// <summary>
    /// Sign in with Google OAuth
    /// </summary>
    Task<bool> SignInWithGoogleAsync();
    
    /// <summary>
    /// Sign in with Facebook OAuth (if enabled)
    /// </summary>
    Task<bool> SignInWithFacebookAsync();
    
    /// <summary>
    /// Sign out current user
    /// </summary>
    Task<bool> SignOutAsync();
    
    /// <summary>
    /// Get current authenticated user
    /// </summary>
    Task<User?> GetCurrentUserAsync();
    
    /// <summary>
    /// Check if user is authenticated
    /// </summary>
    Task<bool> IsAuthenticatedAsync();
    
    /// <summary>
    /// Refresh current session
    /// </summary>
    Task<bool> RefreshSessionAsync();
    
    /// <summary>
    /// Send password reset email
    /// </summary>
    Task<bool> SendPasswordResetEmailAsync(string email);
    
    /// <summary>
    /// Update user password
    /// </summary>
    Task<bool> UpdatePasswordAsync(string newPassword);
    
    /// <summary>
    /// Update user profile
    /// </summary>
    Task<bool> UpdateUserProfileAsync(Dictionary<string, object> updates);
    
    /// <summary>
    /// Get current session info
    /// </summary>
    Task<SupabaseSessionInfo?> GetSessionInfoAsync();
    
    /// <summary>
    /// Verify email with token
    /// </summary>
    Task<bool> VerifyEmailAsync(string token);
    


     /// <summary>
    /// Resend verification email
    /// </summary>
    Task<bool> ResendVerificationEmailAsync(string email);
}
