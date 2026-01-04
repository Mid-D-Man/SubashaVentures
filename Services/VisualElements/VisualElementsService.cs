namespace SubashaVentures.Services.VisualElements;

using System.Collections.Concurrent;
using Microsoft.JSInterop;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Utilities.Constants;
using SubashaVentures.Services.Storage;

/// <summary>
/// Service for managing visual elements like icons and SVGs across the application
/// Handles case-insensitive file lookups with fallback strategies
/// </summary>
public class VisualElementsService : IVisualElementsService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IBlazorAppLocalStorageService _localStorage;
    private readonly ILogger<VisualElementsService> _logger;
    private readonly HttpClient _httpClient;
    
    // Cache for loaded assets
    private readonly ConcurrentDictionary<string, string> _iconCache = new();
    private readonly ConcurrentDictionary<string, string> _svgCache = new();
    
    // Base URL for assets
    private string _baseUrl = string.Empty;
    
    public VisualElementsService(
        IJSRuntime jsRuntime,
        IBlazorAppLocalStorageService localStorage,
        ILogger<VisualElementsService> logger,
        HttpClient httpClient)
    {
        _jsRuntime = jsRuntime;
        _localStorage = localStorage;
        _logger = logger;
        _httpClient = httpClient;
    }

    // ===== INITIALIZATION =====
    
    private async Task EnsureInitializedAsync()
    {
        if (string.IsNullOrEmpty(_baseUrl))
        {
            try
            {
                _baseUrl = await _jsRuntime.InvokeAsync<string>("eval", "window.location.origin");
                _logger.LogInformation($"✓ VisualElementsService initialized with base URL: {_baseUrl}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"⚠ Could not get base URL: {ex.Message}");
                _baseUrl = string.Empty;
            }
        }
    }

    // ===== ICON METHODS =====
    
    // Services/VisualElements/VisualElementsService.cs - SIMPLIFIED INITIALIZATION
    public async Task<string> GetIconAsync(IconType iconType, IconSize size, bool useFallback = true)
    {
        var cacheKey = $"{VisualElementsConstants.CACHE_KEY_PREFIX}icon_{iconType}_{(int)size}";
    
        // Check cache first
        if (_iconCache.TryGetValue(cacheKey, out var cachedUrl))
        {
            return cachedUrl;
        }
    
        // ✅ Use RELATIVE paths - HttpClient already has BaseAddress
        var iconPath = VisualElementsConstants.GetIconPath(iconType, size);
    
        try
        {
            // ✅ Try primary path first (UPPERCASE - matches actual files)
            var response = await _httpClient.GetAsync(iconPath, HttpCompletionOption.ResponseHeadersRead);
        
            if (response.IsSuccessStatusCode)
            {
                var fullUrl = $"{_httpClient.BaseAddress}{iconPath.TrimStart('/')}";
                _iconCache.TryAdd(cacheKey, fullUrl);
                _logger.LogDebug($"✓ Icon cached: {iconPath}");
                return fullUrl;
            }
        
            // ✅ Fallback: try lowercase
            var lowerPath = iconPath.Replace("SBV_ICON", "sbv_icon");
            response = await _httpClient.GetAsync(lowerPath, HttpCompletionOption.ResponseHeadersRead);
        
            if (response.IsSuccessStatusCode)
            {
                var fullUrl = $"{_httpClient.BaseAddress}{lowerPath.TrimStart('/')}";
                _iconCache.TryAdd(cacheKey, fullUrl);
                _logger.LogDebug($"✓ Icon cached (lowercase): {lowerPath}");
                return fullUrl;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"❌ Failed to load icon {iconPath}: {ex.Message}");
        }
    
        _logger.LogWarning($"⚠ Icon not found for {iconType} at size {(int)size}");
        return useFallback ? VisualElementsConstants.FALLBACK_ICON : string.Empty;
    }
    
    /// <summary>
    /// Generate path variations to handle case-sensitivity issues across platforms
    /// Actual files are UPPERCASE based on manifest.json
    /// </summary>
    private List<string> GetIconPathVariations(IconType iconType, IconSize size)
    {
        var variations = new List<string>();
        var basePath = VisualElementsConstants.ICONS_BASE_PATH;
        var sizeValue = (int)size;
        
        switch (iconType)
        {
            case IconType.SBV_ICON:
                // Primary: Uppercase (matches actual files in project)
                variations.Add($"{basePath}/SBV_ICON_{sizeValue}.png");
                
                // Fallback: Lowercase
                variations.Add($"{basePath}/sbv_icon_{sizeValue}.png");
                
                // Fallback: Mixed case
                variations.Add($"{basePath}/Sbv_Icon_{sizeValue}.png");
                break;
                
            default:
                throw new ArgumentOutOfRangeException(nameof(iconType));
        }
        
        return variations;
    }
    
    public async Task<string> GetIconAsync(IconType iconType)
    {
        return await GetIconAsync(iconType, VisualElementsConstants.DEFAULT_ICON_SIZE);
    }
    
    public async Task<bool> IconExistsAsync(IconType iconType, IconSize size)
    {
        try
        {
            var pathVariations = GetIconPathVariations(iconType, size);
            
            foreach (var iconPath in pathVariations)
            {
                var fullUrl = string.IsNullOrEmpty(_baseUrl) ? iconPath : $"{_baseUrl}/{iconPath}";
                
                try
                {
                    var response = await _httpClient.GetAsync(fullUrl, HttpCompletionOption.ResponseHeadersRead);
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                }
                catch
                {
                    // Continue to next variation
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"Icon existence check failed: {ex.Message}");
            return false;
        }
    }
    
    public List<IconSize> GetAvailableSizes(IconType iconType)
    {
        // All SBV_ICON sizes are available based on manifest.json
        return iconType switch
        {
            IconType.SBV_ICON => new List<IconSize>
            {
                IconSize.Size_72,
                IconSize.Size_96,
                IconSize.Size_128,
                IconSize.Size_144,
                IconSize.Size_152,
                IconSize.Size_192,
                IconSize.Size_384,
                IconSize.Size_512,
                IconSize.Size_1024
            },
            _ => new List<IconSize>()
        };
    }

    // ===== SPECIFIC SBV_ICON HELPERS =====
    
    public string GetSBVIcon_72() => BuildIconUrl(IconType.SBV_ICON, IconSize.Size_72);
    public string GetSBVIcon_96() => BuildIconUrl(IconType.SBV_ICON, IconSize.Size_96);
    public string GetSBVIcon_128() => BuildIconUrl(IconType.SBV_ICON, IconSize.Size_128);
    public string GetSBVIcon_144() => BuildIconUrl(IconType.SBV_ICON, IconSize.Size_144);
    public string GetSBVIcon_152() => BuildIconUrl(IconType.SBV_ICON, IconSize.Size_152);
    public string GetSBVIcon_192() => BuildIconUrl(IconType.SBV_ICON, IconSize.Size_192);
    public string GetSBVIcon_384() => BuildIconUrl(IconType.SBV_ICON, IconSize.Size_384);
    public string GetSBVIcon_512() => BuildIconUrl(IconType.SBV_ICON, IconSize.Size_512);
    public string GetSBVIcon_1024() => BuildIconUrl(IconType.SBV_ICON, IconSize.Size_1024);
    
    /// <summary>
    /// Build icon URL using primary case (UPPERCASE - matches actual files)
    /// </summary>
    private string BuildIconUrl(IconType iconType, IconSize size)
    {
        var iconPath = VisualElementsConstants.GetIconPath(iconType, size);
        return string.IsNullOrEmpty(_baseUrl) ? iconPath : $"{_baseUrl}/{iconPath}";
    }

    // ===== SVG METHODS =====
    
    public async Task<string> GetSvgAsync(SvgType svgType, bool useFallback = true)
    {
        if (svgType == SvgType.None)
        {
            return useFallback ? VisualElementsConstants.FALLBACK_SVG : string.Empty;
        }
        
        await EnsureInitializedAsync();
        
        var cacheKey = $"{VisualElementsConstants.CACHE_KEY_PREFIX}svg_{svgType}";
        
        // Check cache first
        if (_svgCache.TryGetValue(cacheKey, out var cachedSvg))
        {
            return cachedSvg;
        }
        
        // Try multiple case variations
        var pathVariations = GetSvgPathVariations(svgType);
        
        foreach (var svgPath in pathVariations)
        {
            var fullUrl = string.IsNullOrEmpty(_baseUrl) ? svgPath : $"{_baseUrl}/{svgPath}";
            
            try
            {
                var svgContent = await _httpClient.GetStringAsync(fullUrl);
                
                if (!string.IsNullOrEmpty(svgContent))
                {
                    _svgCache.TryAdd(cacheKey, svgContent);
                    _logger.LogDebug($"✓ SVG cached: {svgPath}");
                    return svgContent;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"❌ Failed to load SVG variation {svgPath}: {ex.Message}");
            }
        }
        
        _logger.LogWarning($"⚠ SVG not found for {svgType} (tried {pathVariations.Count} variations)");
        return useFallback ? VisualElementsConstants.FALLBACK_SVG : string.Empty;
    }
    
    /// <summary>
    /// Generate path variations for SVG files
    /// </summary>
    private List<string> GetSvgPathVariations(SvgType svgType)
    {
        var variations = new List<string>();
        var basePath = VisualElementsConstants.SVGS_BASE_PATH;
        var enumName = svgType.ToString();
        
        // Try: lowercase, original case, uppercase
        variations.Add($"{basePath}/{enumName.ToLowerInvariant()}.svg");
        variations.Add($"{basePath}/{enumName}.svg");
        variations.Add($"{basePath}/{enumName.ToUpperInvariant()}.svg");
        
        return variations;
    }
    
    public async Task<string> GetSvgByNameAsync(string name, bool useFallback = true)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return useFallback ? VisualElementsConstants.FALLBACK_SVG : string.Empty;
        }
        
        await EnsureInitializedAsync();
        
        var cacheKey = $"{VisualElementsConstants.CACHE_KEY_PREFIX}svg_{name.ToLowerInvariant()}";
        
        // Check cache first
        if (_svgCache.TryGetValue(cacheKey, out var cachedSvg))
        {
            return cachedSvg;
        }
        
        // Try multiple case variations
        var basePath = VisualElementsConstants.SVGS_BASE_PATH;
        var pathVariations = new List<string>
        {
            $"{basePath}/{name.ToLowerInvariant()}.svg",
            $"{basePath}/{name}.svg",
            $"{basePath}/{name.ToUpperInvariant()}.svg",
            $"{basePath}/{char.ToUpperInvariant(name[0]) + name.Substring(1).ToLowerInvariant()}.svg"
        };
        
        foreach (var svgPath in pathVariations)
        {
            var fullUrl = string.IsNullOrEmpty(_baseUrl) ? svgPath : $"{_baseUrl}/{svgPath}";
            
            try
            {
                var svgContent = await _httpClient.GetStringAsync(fullUrl);
                
                if (!string.IsNullOrEmpty(svgContent))
                {
                    _svgCache.TryAdd(cacheKey, svgContent);
                    _logger.LogDebug($"✓ SVG cached: {svgPath}");
                    return svgContent;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"❌ Failed to load SVG variation {svgPath}: {ex.Message}");
            }
        }
        
        _logger.LogWarning($"⚠ SVG not found for name '{name}' (tried {pathVariations.Count} variations)");
        return useFallback ? VisualElementsConstants.FALLBACK_SVG : string.Empty;
    }
    
    public async Task<bool> SvgExistsAsync(SvgType svgType)
    {
        if (svgType == SvgType.None)
            return false;
        
        try
        {
            var pathVariations = GetSvgPathVariations(svgType);
            
            foreach (var svgPath in pathVariations)
            {
                var fullUrl = string.IsNullOrEmpty(_baseUrl) ? svgPath : $"{_baseUrl}/{svgPath}";
                
                try
                {
                    var response = await _httpClient.GetAsync(fullUrl, HttpCompletionOption.ResponseHeadersRead);
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                }
                catch
                {
                    // Continue to next variation
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"SVG existence check failed: {ex.Message}");
            return false;
        }
    }
    
    public string GenerateSvg(string svgMarkup, int width = 24, int height = 24, string? viewBox = null, string? additionalAttributes = null)
    {
        if (string.IsNullOrWhiteSpace(svgMarkup))
        {
            _logger.LogWarning("⚠ Empty SVG markup provided");
            return VisualElementsConstants.FALLBACK_SVG;
        }
        
        var actualViewBox = viewBox ?? $"0 0 {width} {height}";
        var attributes = !string.IsNullOrWhiteSpace(additionalAttributes) ? $" {additionalAttributes}" : string.Empty;
        
        return $"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='{actualViewBox}'{attributes}>{svgMarkup}</svg>";
    }
    
    public string GenerateSvgDataUri(string svgMarkup)
    {
        if (string.IsNullOrWhiteSpace(svgMarkup))
        {
            _logger.LogWarning("⚠ Empty SVG markup for data URI");
            return VisualElementsConstants.FALLBACK_ICON;
        }
        
        // URL encode the SVG for data URI
        var encoded = Uri.EscapeDataString(svgMarkup)
            .Replace("%20", " ")
            .Replace("%3D", "=")
            .Replace("%3A", ":")
            .Replace("%2F", "/")
            .Replace("%3C", "<")
            .Replace("%3E", ">")
            .Replace("%22", "'");
        
        return $"data:image/svg+xml,{encoded}";
    }

    // ===== CACHE METHODS =====
    
    public void ClearCache()
    {
        var iconCount = _iconCache.Count;
        var svgCount = _svgCache.Count;
        
        _iconCache.Clear();
        _svgCache.Clear();
        
        _logger.LogInformation($"🧹 Cache cleared: {iconCount} icons, {svgCount} SVGs");
    }
    
    public async Task PreloadCommonAssetsAsync()
    {
        _logger.LogInformation("📦 Preloading common assets...");
        
        var tasks = new List<Task>();
        
        // Preload icons
        foreach (var (type, size) in VisualElementsConstants.PRELOAD_ICONS)
        {
            tasks.Add(GetIconAsync(type, size));
        }
        
        // Preload SVGs
        foreach (var svgType in VisualElementsConstants.PRELOAD_SVGS)
        {
            tasks.Add(GetSvgAsync(svgType));
        }
        
        await Task.WhenAll(tasks);
        
        _logger.LogInformation($"✓ Preloaded {tasks.Count} assets");
    }
    
    public (int IconsCached, int SvgsCached, int TotalCached) GetCacheStats()
    {
        var iconsCached = _iconCache.Count;
        var svgsCached = _svgCache.Count;
        var totalCached = iconsCached + svgsCached;
        
        return (iconsCached, svgsCached, totalCached);
    }
}
