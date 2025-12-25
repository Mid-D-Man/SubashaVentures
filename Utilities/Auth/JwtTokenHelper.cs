// Utilities/Auth/JwtTokenHelper.cs - JWT TOKEN VALIDATION
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SubashaVentures.Utilities.Auth;

public static class JwtTokenHelper
{
    private const string SupabaseUrl = "https://wbwmovtewytjibxutssk.supabase.co";
    private const string JwtSecret = "fBjc8k1Ou+b1UoLv9zH5v/K5Q1qVpGOPFxATJYYaH9u3U5nH2GxAy7S8hB8pqQxYB/9MUNGaTUr2DZkZqI3pIg=="; // Get from Supabase Project Settings -> API -> JWT Secret

    public static List<Claim>? ValidateAndExtractClaims(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)),
                ValidateIssuer = true,
                ValidIssuer = $"{SupabaseUrl}/auth/v1",
                ValidateAudience = true,
                ValidAudience = "authenticated",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            
            var jwtToken = (JwtSecurityToken)validatedToken;
            var claims = jwtToken.Claims.ToList();

            // Add standard claims if they don't exist
            if (!claims.Any(c => c.Type == ClaimTypes.Email))
            {
                var email = claims.FirstOrDefault(c => c.Type == "email")?.Value;
                if (!string.IsNullOrEmpty(email))
                {
                    claims.Add(new Claim(ClaimTypes.Email, email));
                }
            }

            if (!claims.Any(c => c.Type == ClaimTypes.NameIdentifier))
            {
                var sub = claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                if (!string.IsNullOrEmpty(sub))
                {
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, sub));
                }
            }

            // Extract user metadata
            var userMetadata = claims.FirstOrDefault(c => c.Type == "user_metadata")?.Value;
            if (!string.IsNullOrEmpty(userMetadata))
            {
                try
                {
                    var metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(userMetadata);
                    if (metadata != null)
                    {
                        if (metadata.ContainsKey("first_name"))
                        {
                            claims.Add(new Claim(ClaimTypes.GivenName, metadata["first_name"].ToString()!));
                        }
                        if (metadata.ContainsKey("last_name"))
                        {
                            claims.Add(new Claim(ClaimTypes.Surname, metadata["last_name"].ToString()!));
                        }
                    }
                }
                catch
                {
                    // Ignore metadata parsing errors
                }
            }

            // Add role claim (default to "user" if not present)
            if (!claims.Any(c => c.Type == ClaimTypes.Role))
            {
                var role = claims.FirstOrDefault(c => c.Type == "role")?.Value ?? "user";
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            return claims;
        }
        catch (SecurityTokenExpiredException)
        {
            Console.WriteLine("❌ Token expired");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Token validation failed: {ex.Message}");
            return null;
        }
    }
}
