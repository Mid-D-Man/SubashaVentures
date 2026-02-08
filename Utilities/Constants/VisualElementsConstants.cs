namespace SubashaVentures.Utilities.Constants;

using SubashaVentures.Domain.Enums;

/// <summary>
/// Constants for visual elements (icons, SVGs, etc.)
/// </summary>
public static class VisualElementsConstants
{
    // ===== BASE PATHS =====
    public const string ICONS_BASE_PATH = "icons";
    public const string SVGS_BASE_PATH = "svgs";
    
    // ===== CACHE KEYS =====
    public const string CACHE_KEY_PREFIX = "visual_";
    public const string ICON_CACHE_PREFIX = "visual_icon_";
    public const string SVG_CACHE_PREFIX = "visual_svg_";
    
    // ===== DEFAULT VALUES =====
    public static readonly IconSize DEFAULT_ICON_SIZE = IconSize.Size_192;
    public static readonly SvgSize DEFAULT_SVG_SIZE = SvgSize.Medium;
    
    // ===== FALLBACKS =====
    public const string FALLBACK_ICON = "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'%3E%3Cpath fill='%23ccc' d='M0 0h24v24H0z'/%3E%3C/svg%3E";
    public const string FALLBACK_SVG = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'><rect fill='#ccc' width='24' height='24'/></svg>";
    
    // ===== SVG FILE NAMING =====
    private static readonly Dictionary<SvgType, string> SVG_FILE_NAMES = new()
    {
        { SvgType.About, "SVG_About" },
        { SvgType.Close, "SVG_Close" },
        { SvgType.Search, "SVG_Search" },
        { SvgType.Settings, "SVG_Settings" },
        { SvgType.Warning, "SVG_Warning" },
        { SvgType.CheckMark, "SVG_CheckMark" },
        { SvgType.Star, "SVG_Star" },
        { SvgType.ThumbsUp, "SVG_ThumbsUp" },
        { SvgType.Flame, "SVG_Flame" },
        { SvgType.Home, "SVG_Home" },
        { SvgType.Home2, "SVG_Home2" },
        { SvgType.User, "SVG_User" },
        { SvgType.AddUser, "SVG_Add_User" },
        { SvgType.RemoveUser, "SVG_Remove_User" },
        { SvgType.AdminUser, "SVG_Admin_User" },
        { SvgType.Cart, "SVG_Cart" },
        { SvgType.Wishlist, "SVG_Wishlist" },
        { SvgType.AllProducts, "SVG_AllProducts" },
        { SvgType.ShopNow, "SVG_ShopNow" },
        { SvgType.Order, "SVG_Order" },
        { SvgType.TrackOrders, "SVG_TrackOrders" },
        { SvgType.Beddings, "SVG_Beddings" },
        { SvgType.ChildrenClothes, "SVG_ChildrenClothes" },
        { SvgType.Dress, "SVG_Dress" },
        { SvgType.Tuxido, "SVG_Tuxido" },
        { SvgType.Mail, "SVG_Mail" },
        { SvgType.Messages, "SVG_Messages" },
        { SvgType.Contact, "SVG_Contact" },
        { SvgType.ContactUs, "SVG_ContactUs" },
        { SvgType.HelpCenter, "SVG_HelpCenter" },
        { SvgType.Address, "SVG_Address" },
        { SvgType.Payment, "SVG_Payment" },
        { SvgType.History, "SVG_History" },
        { SvgType.History2, "SVG_History2" },
        { SvgType.Records, "SVG_Records" },
        { SvgType.Notification, "SVG_Notification" },
        { SvgType.Stats, "SVG_Stats" },
        { SvgType.Reports, "SVG_Reports" },
        { SvgType.Offer, "SVG_Offer" },
        { SvgType.Story, "SVG_Story" },
        { SvgType.Sv, "SVG_Sv" },
        { SvgType.NoConnection, "SVG_No_Connection" },
        { SvgType.Heart, "SVG_Heart" }
    };
    
    /// <summary>
    /// Get the filename for a given SVG type
    /// </summary>
    public static string GetSvgFileName(SvgType svgType)
    {
        return SVG_FILE_NAMES.TryGetValue(svgType, out var fileName) 
            ? fileName 
            : svgType.ToString();
    }
    
    /// <summary>
    /// Get the full path for a given SVG type
    /// </summary>
    public static string GetSvgPath(SvgType svgType)
    {
        return $"{SVGS_BASE_PATH}/{GetSvgFileName(svgType)}.svg";
    }
    
    /// <summary>
    /// Get icon path for a given type and size
    /// </summary>
    public static string GetIconPath(IconType iconType, IconSize size)
    {
        return iconType switch
        {
            IconType.SBV_ICON => $"{ICONS_BASE_PATH}/SBV_ICON_{(int)size}.png",
            _ => throw new ArgumentOutOfRangeException(nameof(iconType))
        };
    }
    
    // ===== PRELOAD LISTS =====
    
    /// <summary>
    /// Icons to preload at startup
    /// </summary>
    public static readonly List<(IconType type, IconSize size)> PRELOAD_ICONS = new()
    {
        (IconType.SBV_ICON, IconSize.Size_192),
        (IconType.SBV_ICON, IconSize.Size_512)
    };
    
    /// <summary>
    /// SVGs to preload at startup (commonly used icons)
    /// </summary>
    public static readonly List<SvgType> PRELOAD_SVGS = new()
    {
        // Navigation
        SvgType.Home,
        SvgType.Search,
        SvgType.Cart,
        SvgType.User,
        SvgType.Wishlist,
        
        // Common UI
        SvgType.Close,
        SvgType.Settings,
        SvgType.Notification,
        SvgType.CheckMark,
        SvgType.Warning,
        
        // Shopping
        SvgType.AllProducts,
        SvgType.Order,
        SvgType.Heart,
        
        // Account
        SvgType.Address,
        SvgType.Payment,
        SvgType.History
    };
    
    /// <summary>
    /// SVG color manipulation helpers
    /// </summary>
    public static class SvgColors
    {
        public const string PRIMARY = "#96994A";
        public const string PRIMARY_DARK = "#B8BB6A";
        public const string WHITE = "#FFFFFF";
        public const string BLACK = "#000000";
        public const string GRAY = "#6B7280";
        public const string TRANSPARENT = "transparent";
    }
    
    /// <summary>
    /// Common SVG transformations
    /// </summary>
    public static class SvgTransforms
    {
        public const string ROTATE_90 = "rotate(90)";
        public const string ROTATE_180 = "rotate(180)";
        public const string ROTATE_270 = "rotate(270)";
        public const string FLIP_HORIZONTAL = "scale(-1, 1)";
        public const string FLIP_VERTICAL = "scale(1, -1)";
    }
}
