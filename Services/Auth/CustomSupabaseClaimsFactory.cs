// Services/Auth/CustomSupabaseClaimsFactory.cs - FIXED TO AVOID DATABASE QUERY DURING AUTH
using System.Security.Claims;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Gotrue;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;
using Client = Supabase.Client;

namespace SubashaVentures.Services.Auth;

/// <summary>
/// Custom claims factory - UPDATED to get role from JWT metadata, NOT database
/// This prevents infinite recursion during authentication
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
    /// Create ClaimsPrincipal with user role from JWT metadata (NOT database)
    /// CRITICAL: This method must NOT query the database to avoid infinite recursion
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

            // ✅ CRITICAL FIX: Get role from JWT metadata, NOT from database
            // This prevents the infinite recursion issue
            string role = "user"; // Default role

            try
            {
                // Try to get role from user metadata (set during signup/OAuth)
                if (user.UserMetadata != null && user.UserMetadata.ContainsKey("role"))
                {
                    role = user.UserMetadata["role"]?.ToString() ?? "user";
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✅ Role found in JWT metadata: {role}",
                        LogLevel.Info
                    );
                }
                else
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "⚠️ Role not found in JWT metadata, using default 'user'",
                        LogLevel.Warning
                    );
                    
                    // Schedule a background task to update metadata from database
                    _ = Task.Run(async () => await UpdateUserMetadataFromDatabaseAsync(user.Id));
                }
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, "Getting role from metadata");
                _logger.LogWarning(ex, "Failed to get role from metadata, using default");
            }
            
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
    /// Background task to update user metadata from database
    /// This runs AFTER authentication is complete, so no recursion
    /// </summary>
    private async Task UpdateUserMetadataFromDatabaseAsync(string userId)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"🔄 Background: Syncing role from database for user: {userId}",
                LogLevel.Info
            );

            // Wait a bit to ensure authentication is fully complete
            await Task.Delay(2000);

            var user = await _supabaseClient
                .From<UserModel>()
                .Where(u => u.Id == userId)
                .Single();

            if (user == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"⚠️ User not found in user_data table: {userId}",
                    LogLevel.Warning
                );
                return;
            }

            // Update the user's metadata in Supabase Auth
            var currentUser = _supabaseClient.Auth.CurrentUser;
            if (currentUser != null && currentUser.Id == userId)
            {
                var updates = new Dictionary<string, object>
                {
                    { "role", user.Role }
                };

                var attributes = new UserAttributes
                {
                    Data = updates
                };

                await _supabaseClient.Auth.Update(attributes);

                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ Background: Updated JWT metadata with role: {user.Role}",
                    LogLevel.Info
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Background role sync");
            _logger.LogWarning(ex, "Failed to sync role from database (non-critical)");
        }
    }
}