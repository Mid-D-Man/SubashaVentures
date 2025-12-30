using System.Security.Claims;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Gotrue;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;
using Client = Supabase.Client;

namespace SubashaVentures.Services.Auth;

/// <summary>
/// Custom claims factory - UPDATED to get role from users table directly
/// </summary>
public class CustomSupabaseClaimsFactory
{
    private readonly Client _supabaseClient;
    private readonly ILogger<CustomSupabaseClaimsFactory> _logger;

    public CustomSupabaseClaimsFactory(
        Client supabaseClient,
        ILogger<CustomSupabaseClaimsFactory> logger)
    {
        _supabaseClient = supabaseClient;
        _logger = logger;
    }

    /// <summary>
    /// Create ClaimsPrincipal with user role from users table
    /// </summary>
    public async Task<ClaimsPrincipal> CreateUserPrincipalAsync(User user)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Creating claims principal for user: {user.Email}",
                LogLevel.Info
            );

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim("sub", user.Id),
            };

            // ✅ Get role from users table (single field)
            var role = await GetUserRoleAsync(user.Id);
            
            claims.Add(new Claim(ClaimTypes.Role, role));
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Added role claim: {role} for user: {user.Email}",
                LogLevel.Info
            );

            // Add metadata from user profile
            if (user.UserMetadata != null)
            {
                if (user.UserMetadata.TryGetValue("first_name", out var firstName))
                    claims.Add(new Claim(ClaimTypes.GivenName, firstName?.ToString() ?? ""));
                
                if (user.UserMetadata.TryGetValue("last_name", out var lastName))
                    claims.Add(new Claim(ClaimTypes.Surname, lastName?.ToString() ?? ""));
                
                if (user.UserMetadata.TryGetValue("avatar_url", out var avatar))
                    claims.Add(new Claim("avatar_url", avatar?.ToString() ?? ""));
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Claims created for {user.Email} with role: {role}",
                LogLevel.Info
            );

            var identity = new ClaimsIdentity(claims, "Supabase");
            var principal = new ClaimsPrincipal(identity);

            return principal;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Creating claims principal");
            _logger.LogError(ex, "Failed to create claims principal for user: {UserId}", user.Id);
            
            return new ClaimsPrincipal(new ClaimsIdentity());
        }
    }

    /// <summary>
    /// Get user role from users table (single field query)
    /// </summary>
    private async Task<string> GetUserRoleAsync(string userId)
    {
        try
        {
            var user = await _supabaseClient
                .From<UserModel>()
                .Where(u => u.Id == userId)
                .Single();

            if (user == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"⚠️ User not found in users table: {userId}, assigning default 'user' role",
                    LogLevel.Warning
                );
                return "user";
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Retrieved role for user {userId}: {user.Role}",
                LogLevel.Info
            );

            return user.Role;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting role for user: {userId}");
            _logger.LogError(ex, "Failed to get user role for: {UserId}", userId);
            return "user"; // Default to 'user' on error
        }
    }
}
