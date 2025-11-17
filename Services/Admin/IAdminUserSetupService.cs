// Services/Admin/IAdminUserSetupService.cs - NEW INTERFACE
namespace SubashaVentures.Services.Admin;

/// <summary>
/// Service for setting up and managing the predefined admin user
/// </summary>
public interface IAdminUserSetupService
{
    /// <summary>
    /// Ensures the admin user exists in the system (called on startup)
    /// </summary>
    Task<bool> EnsureAdminUserExistsAsync();
    
    /// <summary>
    /// Check if a user email is the admin user
    /// </summary>
    Task<bool> IsAdminUserAsync(string email);
    
    /// <summary>
    /// Get admin user details
    /// </summary>
    Task<AdminUserInfo?> GetAdminUserInfoAsync();
}

/// <summary>
/// Admin user information
/// </summary>
public class AdminUserInfo
{
    public string Email { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
