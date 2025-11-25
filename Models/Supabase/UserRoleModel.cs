// Models/Supabase/UserRoleModel.cs - UPDATED (2 roles only)
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SubashaVentures.Models.Supabase;

/// <summary>
/// User role model for RBAC (2 roles: user, superior_admin)
/// Maps to public.user_roles table
/// </summary>
[Table("user_roles")]
public class UserRoleModel : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public string Id { get; set; } = string.Empty;
    
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;
    
    [Column("role")]
    public string Role { get; set; } = "user"; // user or superior_admin
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("created_by")]
    public string? CreatedBy { get; set; }
    
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// User roles enum (ONLY 2 ROLES)
/// </summary>
public enum UserRole
{
    User,
    SuperiorAdmin
}

/// <summary>
/// Extension methods for role checking
/// </summary>
public static class UserRoleExtensions
{
    public static string ToRoleString(this UserRole role)
    {
        return role switch
        {
            UserRole.SuperiorAdmin => "superior_admin",
            UserRole.User => "user",
            _ => "user"
        };
    }
    
    public static UserRole ToUserRole(this string role)
    {
        return role?.ToLower() switch
        {
            "superior_admin" => UserRole.SuperiorAdmin,
            _ => UserRole.User
        };
    }
    
    public static bool IsSuperiorAdmin(this string role)
    {
        return role?.ToLower() == "superior_admin";
    }
}
