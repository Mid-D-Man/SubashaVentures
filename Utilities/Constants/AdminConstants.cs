
// Utilities/Constants/AdminConstants.cs - NEW
namespace SubashaVentures.Utilities.Constants;


/// <summary>
/// Constants related to admin functionality
/// </summary>
public static class AdminConstants
{
    // Admin user email (predefined)
    public const string ADMIN_EMAIL = "subashaventures.dev@gmail.com";
    
    // Admin roles
    public const string SUPER_ADMIN_ROLE = "super_admin";
    public const string ADMIN_ROLE = "admin";
    public const string MODERATOR_ROLE = "moderator";
    
    // Admin permissions
    public static readonly string[] SUPER_ADMIN_PERMISSIONS = 
    {
        "users.view",
        "users.create",
        "users.edit",
        "users.delete",
        "products.view",
        "products.create",
        "products.edit",
        "products.delete",
        "orders.view",
        "orders.edit",
        "orders.cancel",
        "settings.view",
        "settings.edit",
        "analytics.view",
        "messages.view",
        "messages.respond",
        "all"
    };
    
    // Helper method to check if email is admin
    public static bool IsAdminEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;
            
        return email.Equals(ADMIN_EMAIL, StringComparison.OrdinalIgnoreCase);
    }
}
