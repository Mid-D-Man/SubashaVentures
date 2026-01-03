namespace SubashaVentures.Utilities.Constants;

using SubashaVentures.Domain.Enums;

/// <summary>
/// Constants for visual elements (icons, SVGs, etc.)
/// </summary>
public static class VisualElementsConstants
{
    // ===== PATHS =====
    public const string ICONS_BASE_PATH = "icons";
    public const string SVGS_BASE_PATH = "svg";
    
    // ===== ICON NAMING =====
    public const string SBV_ICON_PREFIX = "SBV_ICON";
    
    // ===== DEFAULT SIZES =====
    public const IconSize DEFAULT_ICON_SIZE = IconSize.Size_192;
    public const int DEFAULT_SVG_WIDTH = 24;
    public const int DEFAULT_SVG_HEIGHT = 24;
    
    // ===== FALLBACK =====
    /// <summary>
    /// Inline SVG fallback when icon not found
    /// </summary>
    public const string FALLBACK_ICON = "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'%3E%3Crect width='100' height='100' fill='%2396994A'/%3E%3Ctext x='50' y='50' font-size='48' fill='white' text-anchor='middle' dominant-baseline='middle' font-weight='bold'%3ESV%3C/text%3E%3C/svg%3E";
    
    /// <summary>
    /// Generic SVG fallback
    /// </summary>
    public const string FALLBACK_SVG = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><circle cx='12' cy='12' r='10'/><line x1='12' y1='8' x2='12' y2='12'/><line x1='12' y1='16' x2='12.01' y2='16'/></svg>";
    
    // ===== CACHE SETTINGS =====
    public const int CACHE_EXPIRATION_MINUTES = 60;
    public const string CACHE_KEY_PREFIX = "visual_element_";
    
    // ===== PATH BUILDERS =====
    /// <summary>
    /// Get the full path for an icon
    /// </summary>
    public static string GetIconPath(IconType iconType, IconSize size)
    {
        return $"{ICONS_BASE_PATH}/{GetIconFileName(iconType, size)}";
    }
    
    /// <summary>
    /// Get the filename for an icon
    /// </summary>
    public static string GetIconFileName(IconType iconType, IconSize size)
    {
        return iconType switch
        {
            IconType.SBV_ICON => $"{SBV_ICON_PREFIX}_{(int)size}.png",
            _ => throw new ArgumentOutOfRangeException(nameof(iconType))
        };
    }
    
    /// <summary>
    /// Get the full path for an SVG
    /// </summary>
    public static string GetSvgPath(SvgType svgType)
    {
        if (svgType == SvgType.None)
            return string.Empty;
            
        return $"{SVGS_BASE_PATH}/{svgType.ToString().ToLowerInvariant()}.svg";
    }
    
    // ===== PRELOAD ASSETS =====
    /// <summary>
    /// Icons to preload on app startup
    /// </summary>
    public static readonly (IconType Type, IconSize Size)[] PRELOAD_ICONS = 
    {
        (IconType.SBV_ICON, IconSize.Size_192),
        (IconType.SBV_ICON, IconSize.Size_512)
    };
    
    /// <summary>
    /// SVGs to preload on app startup (currently none)
    /// </summary>
    public static readonly SvgType[] PRELOAD_SVGS = Array.Empty<SvgType>();
}
