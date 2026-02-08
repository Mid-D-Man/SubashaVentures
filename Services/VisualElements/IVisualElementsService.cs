namespace SubashaVentures.Services.VisualElements;

using SubashaVentures.Domain.Enums;

/// <summary>
/// Service for managing visual elements like icons and SVGs across the application
/// </summary>
public interface IVisualElementsService
{
    // ===== INITIALIZATION =====
    
    /// <summary>
    /// Initialize and preload all assets
    /// Must be called at application startup
    /// </summary>
    Task InitializeAsync();
    
    /// <summary>
    /// Check if service has been initialized
    /// </summary>
    bool IsInitialized { get; }
    
    // ===== ICON METHODS =====
    
    /// <summary>
    /// Get an icon URL by type and size
    /// </summary>
    Task<string> GetIconAsync(IconType iconType, IconSize size, bool useFallback = true);
    
    /// <summary>
    /// Get icon with default size (192px)
    /// </summary>
    Task<string> GetIconAsync(IconType iconType);
    
    /// <summary>
    /// Check if an icon exists
    /// </summary>
    Task<bool> IconExistsAsync(IconType iconType, IconSize size);
    
    /// <summary>
    /// Get all available icon sizes for a given icon type
    /// </summary>
    List<IconSize> GetAvailableSizes(IconType iconType);
    
    // ===== SPECIFIC SBV_ICON HELPERS =====
    string GetSBVIcon_72();
    string GetSBVIcon_96();
    string GetSBVIcon_128();
    string GetSBVIcon_144();
    string GetSBVIcon_152();
    string GetSBVIcon_192();
    string GetSBVIcon_384();
    string GetSBVIcon_512();
    string GetSBVIcon_1024();
    
    // ===== SVG METHODS =====
    
    /// <summary>
    /// Get an SVG by type (returns raw SVG markup)
    /// </summary>
    Task<string> GetSvgAsync(SvgType svgType, bool useFallback = true);
    
    /// <summary>
    /// Get an SVG with custom size
    /// </summary>
    Task<string> GetSvgAsync(SvgType svgType, int width, int height, bool useFallback = true);
    
    /// <summary>
    /// Get an SVG with preset size
    /// </summary>
    Task<string> GetSvgAsync(SvgType svgType, SvgSize size, bool useFallback = true);
    
    /// <summary>
    /// Get an SVG with custom color
    /// </summary>
    Task<string> GetSvgWithColorAsync(SvgType svgType, string color, bool useFallback = true);
    
    /// <summary>
    /// Get an SVG with custom size and color
    /// </summary>
    Task<string> GetSvgWithColorAsync(SvgType svgType, int width, int height, string color, bool useFallback = true);
    
    /// <summary>
    /// Get an SVG with multiple customizations
    /// </summary>
    Task<string> GetCustomSvgAsync(
        SvgType svgType,
        int? width = null,
        int? height = null,
        string? fillColor = null,
        string? strokeColor = null,
        string? className = null,
        string? transform = null,
        bool useFallback = true);
    
    /// <summary>
    /// Get SVG by name (for dynamically loaded SVGs)
    /// </summary>
    Task<string> GetSvgByNameAsync(string name, bool useFallback = true);
    
    /// <summary>
    /// Check if an SVG exists
    /// </summary>
    Task<bool> SvgExistsAsync(SvgType svgType);
    
    // ===== SVG MANIPULATION =====
    
    /// <summary>
    /// Change fill color of SVG markup
    /// </summary>
    string ChangeSvgColor(string svgMarkup, string color);
    
    /// <summary>
    /// Change stroke color of SVG markup
    /// </summary>
    string ChangeSvgStroke(string svgMarkup, string strokeColor, int? strokeWidth = null);
    
    /// <summary>
    /// Resize SVG markup
    /// </summary>
    string ResizeSvg(string svgMarkup, int width, int height);
    
    /// <summary>
    /// Add class to SVG markup
    /// </summary>
    string AddSvgClass(string svgMarkup, string className);
    
    /// <summary>
    /// Apply transform to SVG
    /// </summary>
    string TransformSvg(string svgMarkup, string transform);
    
    // ===== UTILITY METHODS =====
    
    /// <summary>
    /// Generate a custom SVG with given markup
    /// </summary>
    string GenerateSvg(string svgMarkup, int width = 24, int height = 24, string? viewBox = null, string? additionalAttributes = null);
    
    /// <summary>
    /// Generate an inline data URI SVG (useful for backgrounds)
    /// </summary>
    string GenerateSvgDataUri(string svgMarkup);
    
    /// <summary>
    /// Get SVG as Base64 data URI
    /// </summary>
    Task<string> GetSvgAsBase64Async(SvgType svgType);
    
    // ===== CACHE METHODS =====
    
    /// <summary>
    /// Clear the icon/SVG cache
    /// </summary>
    void ClearCache();
    
    /// <summary>
    /// Preload commonly used icons and SVGs into cache
    /// </summary>
    Task PreloadCommonAssetsAsync();
    
    /// <summary>
    /// Preload specific SVGs
    /// </summary>
    Task PreloadSvgsAsync(params SvgType[] svgTypes);
    
    /// <summary>
    /// Get cache statistics
    /// </summary>
    (int IconsCached, int SvgsCached, int TotalCached) GetCacheStats();
}
