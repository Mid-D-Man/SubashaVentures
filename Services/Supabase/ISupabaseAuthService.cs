// Services/Supabase/ISupabaseAuthService.cs - COMPLETE WITH MFA SUPPORT
using SubashaVentures.Models.Supabase;
using Supabase.Gotrue;

namespace SubashaVentures.Services.Supabase;

/// <summary>
/// Service for Supabase authentication operations with OAuth and MFA support
/// </summary>
public interface ISupabaseAuthService
{
    // ==================== SIGN IN/UP ====================
    
    /// <summary>
    /// Sign in with email and password
    /// </summary>
    Task<SupabaseAuthResult> SignInAsync(string email, string password);
    
    /// <summary>
    /// Sign up with email and password
    /// </summary>
    Task<SupabaseAuthResult> SignUpAsync(string email, string password, UserModel userData);
    
    /// <summary>
    /// Sign in with Google OAuth (Pure C# - redirects to Google)
    /// </summary>
    Task<bool> SignInWithGoogleAsync(string? returnUrl = null);
    
    /// <summary>
    /// Handle OAuth callback after redirect
    /// </summary>
    Task<SupabaseAuthResult> HandleOAuthCallbackAsync();
    
    /// <summary>
    /// Sign out current user and clear all session data
    /// </summary>
    Task<bool> SignOutAsync();
    
    // ==================== USER MANAGEMENT ====================
    
    /// <summary>
    /// Get current authenticated user
    /// </summary>
    Task<User?> GetCurrentUserAsync();
    
    /// <summary>
    /// Check if user is authenticated
    /// </summary>
    Task<bool> IsAuthenticatedAsync();
    
    // ==================== SESSION MANAGEMENT ====================
    
    /// <summary>
    /// Get current session
    /// </summary>
    Task<Session?> GetCurrentSessionAsync();
    
    /// <summary>
    /// Refresh current session
    /// </summary>
    Task<bool> RefreshSessionAsync();
    
    // ==================== PASSWORD MANAGEMENT ====================
    
    /// <summary>
    /// Send password reset email
    /// </summary>
    Task<bool> SendPasswordResetEmailAsync(string email);
    
    /// <summary>
    /// Update user password (requires current session)
    /// </summary>
    Task<SupabaseAuthResult> UpdatePasswordAsync(string newPassword);
    
    /// <summary>
    /// Change password (same as UpdatePasswordAsync for consistency)
    /// </summary>
    Task<SupabaseAuthResult> ChangePasswordAsync(string newPassword);
    
    /// <summary>
    /// Reset password with token (from email link)
    /// </summary>
    Task<SupabaseAuthResult> ResetPasswordWithTokenAsync(string token, string newPassword);
    
    // ==================== EMAIL VERIFICATION ====================
    
    /// <summary>
    /// Verify email with token
    /// </summary>
    Task<bool> VerifyEmailAsync(string email,string token);
    
    /// <summary>
    /// Resend verification email
    /// </summary>
    Task<bool> ResendVerificationEmailAsync(string email);
    
    // ==================== PROFILE MANAGEMENT ====================
    
    /// <summary>
    /// Update user profile metadata
    /// </summary>
    Task<bool> UpdateUserProfileAsync(Dictionary<string, object> updates);
    
    // ==================== MFA (MULTI-FACTOR AUTHENTICATION) ====================
    
    /// <summary>
    /// Enroll in MFA (Time-based One-Time Password)
    /// </summary>
    /// <param name="factorType">Type of MFA factor (typically "totp")</param>
    /// <returns>Enrollment result with QR code URL and secret</returns>
    Task<MfaEnrollmentResult> EnrollMfaAsync(string factorType);
    
    /// <summary>
    /// Verify MFA enrollment with code from authenticator app
    /// </summary>
    /// <param name="factorId">Factor ID from enrollment</param>
    /// <param name="code">6-digit code from authenticator app</param>
    /// <returns>Verification result</returns>
    Task<SupabaseAuthResult> VerifyMfaAsync(string factorId, string code);
    
    /// <summary>
    /// Unenroll (disable) MFA factor
    /// </summary>
    /// <param name="factorId">Factor ID to unenroll</param>
    /// <returns>Result of unenrollment</returns>
    Task<SupabaseAuthResult> UnenrollMfaAsync(string factorId);
    
    /// <summary>
    /// Get all enrolled MFA factors for current user
    /// </summary>
    /// <returns>List of enrolled factors</returns>
    Task<List<MfaFactor>?> GetMfaFactorsAsync();
    
    /// <summary>
    /// Challenge MFA (request code verification during sign-in)
    /// </summary>
    /// <param name="factorId">Factor ID to challenge</param>
    /// <returns>Challenge ID for verification</returns>
    Task<string?> ChallengeMfaAsync(string factorId);
    
    /// <summary>
    /// Verify MFA challenge (during sign-in)
    /// </summary>
    /// <param name="factorId">Factor ID being verified</param>
    /// <param name="challengeId">Challenge ID from ChallengeMfaAsync</param>
    /// <param name="code">6-digit code from authenticator app</param>
    /// <returns>Authentication result with session</returns>
    Task<SupabaseAuthResult> VerifyMfaChallengeAsync(string factorId, string challengeId, string code);
}

// ==================== MFA DATA MODELS ====================

/// <summary>
/// Result of MFA enrollment
/// </summary>
public class MfaEnrollmentResult
{
    public bool Success { get; set; }
    public string? FactorId { get; set; }
    public string? QrCodeUrl { get; set; }
    public string? Secret { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// MFA Factor information
/// </summary>
public class MfaFactor
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}