namespace SubashaVentures.Services.VisualElements;

using SubashaVentures.Domain.Enums;

/// <summary>
/// Service for managing visual elements like icons and SVGs across the application
/// </summary>
public interface IVisualElementsService
{
    // ===== ICON METHODS =====
    
    /// <summary>
    /// Get an icon URL by type and size
    /// </summary>
    /// <param name="iconType">Type of icon</param>
    /// <param name="size">Size of icon</param>
    /// <param name="useFallback">Whether to return fallback on error</param>
    /// <returns>Icon URL or fallback</returns>
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
    /// Get an SVG by type
    /// </summary>
    /// <param name="svgType">Type of SVG</param>
    /// <param name="useFallback">Whether to return fallback on error</param>
    /// <returns>SVG markup as string</returns>
    Task<string> GetSvgAsync(SvgType svgType, bool useFallback = true);
    
    /// <summary>
    /// Get SVG by name (for dynamically loaded SVGs)
    /// </summary>
    /// <param name="name">SVG filename without extension</param>
    /// <param name="useFallback">Whether to return fallback on error</param>
    Task<string> GetSvgByNameAsync(string name, bool useFallback = true);
    
    /// <summary>
    /// Check if an SVG exists
    /// </summary>
    Task<bool> SvgExistsAsync(SvgType svgType);
    
    /// <summary>
    /// Generate a custom SVG with given markup
    /// </summary>
    /// <param name="svgMarkup">SVG path/shape markup (inner content)</param>
    /// <param name="width">SVG width</param>
    /// <param name="height">SVG height</param>
    /// <param name="viewBox">Optional custom viewBox (defaults to "0 0 {width} {height}")</param>
    /// <param name="additionalAttributes">Optional additional SVG attributes (e.g., "fill='currentColor'")</param>
    /// <returns>Complete SVG element as string</returns>
    string GenerateSvg(string svgMarkup, int width = 24, int height = 24, string? viewBox = null, string? additionalAttributes = null);
    
    /// <summary>
    /// Generate an inline data URI SVG (useful for backgrounds)
    /// </summary>
    /// <param name="svgMarkup">Complete SVG markup</param>
    /// <returns>Data URI string</returns>
    string GenerateSvgDataUri(string svgMarkup);
    
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
    /// Get cache statistics
    /// </summary>
    (int IconsCached, int SvgsCached, int TotalCached) GetCacheStats();
}
