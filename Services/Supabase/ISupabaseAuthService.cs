// Services/Auth/ISupabaseAuthService.cs - COMPLETE INTERFACE
using SubashaVentures.Models.Supabase;
using Supabase.Gotrue;

namespace SubashaVentures.Services.Auth;

/// <summary>
/// Service for Supabase authentication operations with OAuth support (Pure C#)
/// </summary>
public interface ISupabaseAuthService
{
    // ==================== SIGN IN/UP ====================
    
    /// <summary>
    /// Sign in with email and password
    /// </summary>
    /// <param name="email">User email</param>
    /// <param name="password">User password</param>
    /// <returns>Authentication result with session info</returns>
    Task<SupabaseAuthResult> SignInAsync(string email, string password);
    
    /// <summary>
    /// Sign up with email and password
    /// </summary>
    /// <param name="email">User email</param>
    /// <param name="password">User password</param>
    /// <param name="userData">User profile data</param>
    /// <returns>Authentication result</returns>
    Task<SupabaseAuthResult> SignUpAsync(string email, string password, UserModel userData);
    
    /// <summary>
    /// Sign in with Google OAuth (Pure C# - redirects to Google)
    /// </summary>
    /// <param name="returnUrl">URL to return to after OAuth (optional)</param>
    /// <returns>True if redirect initiated successfully</returns>
    Task<bool> SignInWithGoogleAsync(string? returnUrl = null);
    
    /// <summary>
    /// Handle OAuth callback after redirect
    /// </summary>
    /// <returns>Authentication result</returns>
    Task<SupabaseAuthResult> HandleOAuthCallbackAsync();
    
    /// <summary>
    /// Sign out current user and clear all session data
    /// </summary>
    /// <returns>True if sign out successful</returns>
    Task<bool> SignOutAsync();
    
    // ==================== USER MANAGEMENT ====================
    
    /// <summary>
    /// Get current authenticated user
    /// </summary>
    /// <returns>Current user or null if not authenticated</returns>
    Task<User?> GetCurrentUserAsync();
    
    /// <summary>
    /// Check if user is authenticated
    /// </summary>
    /// <returns>True if authenticated</returns>
    Task<bool> IsAuthenticatedAsync();
    
    // ==================== SESSION MANAGEMENT ====================
    
    /// <summary>
    /// Get current session
    /// </summary>
    /// <returns>Current session or null</returns>
    Task<Session?> GetCurrentSessionAsync();
    
    /// <summary>
    /// Refresh current session
    /// </summary>
    /// <returns>True if refresh successful</returns>
    Task<bool> RefreshSessionAsync();
    
    // ==================== PASSWORD MANAGEMENT ====================
    
    /// <summary>
    /// Send password reset email
    /// </summary>
    /// <param name="email">User email</param>
    /// <returns>True if email sent successfully</returns>
    Task<bool> SendPasswordResetEmailAsync(string email);
    
    /// <summary>
    /// Update user password
    /// </summary>
    /// <param name="newPassword">New password</param>
    /// <returns>Authentication result</returns>
    Task<SupabaseAuthResult> UpdatePasswordAsync(string newPassword);
    
    // ==================== EMAIL VERIFICATION ====================
    
    /// <summary>
    /// Verify email with token
    /// </summary>
    /// <param name="token">Verification token</param>
    /// <returns>True if verification successful</returns>
    Task<bool> VerifyEmailAsync(string token);
    
    /// <summary>
    /// Resend verification email
    /// </summary>
    /// <param name="email">User email</param>
    /// <returns>True if email sent successfully</returns>
    Task<bool> ResendVerificationEmailAsync(string email);
    
    // ==================== PROFILE MANAGEMENT ====================
    
    /// <summary>
    /// Update user profile metadata
    /// </summary>
    /// <param name="updates">Dictionary of updates</param>
    /// <returns>True if update successful</returns>
    Task<bool> UpdateUserProfileAsync(Dictionary<string, object> updates);
}
