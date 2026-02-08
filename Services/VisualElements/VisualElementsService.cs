namespace SubashaVentures.Services.VisualElements;

using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.JSInterop;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Utilities.Constants;
using SubashaVentures.Services.Storage;

/// <summary>
/// Service for managing visual elements like icons and SVGs across the application
/// Preloads assets at startup and provides manipulation capabilities
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
    
    // Initialization flag
    private bool _isInitialized = false;
    
    public bool IsInitialized => _isInitialized;
    
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
    
    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            _logger.LogInformation("VisualElementsService already initialized");
            return;
        }
        
        _logger.LogInformation("🎨 Initializing VisualElementsService...");
        
        try
        {
            // Preload all common assets
            await PreloadCommonAssetsAsync();
            
            _isInitialized = true;
            _logger.LogInformation("✓ VisualElementsService initialized successfully");
            _logger.LogInformation($"   📊 Cache: {_iconCache.Count} icons, {_svgCache.Count} SVGs loaded");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to initialize VisualElementsService");
            throw;
        }
    }

    // ===== ICON METHODS =====
    
    public async Task<string> GetIconAsync(IconType iconType, IconSize size, bool useFallback = true)
    {
        var cacheKey = $"{VisualElementsConstants.ICON_CACHE_PREFIX}{iconType}_{(int)size}";
        
        // Check cache first
        if (_iconCache.TryGetValue(cacheKey, out var cachedUrl))
        {
            return cachedUrl;
        }
        
        var iconPath = VisualElementsConstants.GetIconPath(iconType, size);
        
        try
        {
            var response = await _httpClient.GetAsync(iconPath, HttpCompletionOption.ResponseHeadersRead);
            
            if (response.IsSuccessStatusCode)
            {
                var fullUrl = $"{_httpClient.BaseAddress?.ToString().TrimEnd('/')}/{iconPath.TrimStart('/')}";
                _iconCache.TryAdd(cacheKey, fullUrl);
                return fullUrl;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"❌ Failed to load icon {iconPath}: {ex.Message}");
        }
        
        return useFallback ? VisualElementsConstants.FALLBACK_ICON : string.Empty;
    }
    
    public async Task<string> GetIconAsync(IconType iconType)
    {
        return await GetIconAsync(iconType, VisualElementsConstants.DEFAULT_ICON_SIZE);
    }
    
    public async Task<bool> IconExistsAsync(IconType iconType, IconSize size)
    {
        try
        {
            var iconPath = VisualElementsConstants.GetIconPath(iconType, size);
            var response = await _httpClient.GetAsync(iconPath, HttpCompletionOption.ResponseHeadersRead);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
    
    public List<IconSize> GetAvailableSizes(IconType iconType)
    {
        return iconType switch
        {
            IconType.SBV_ICON => new List<IconSize>
            {
                IconSize.Size_72, IconSize.Size_96, IconSize.Size_128,
                IconSize.Size_144, IconSize.Size_152, IconSize.Size_192,
                IconSize.Size_384, IconSize.Size_512, IconSize.Size_1024
            },
            _ => new List<IconSize>()
        };
    }

    // ===== SBV_ICON HELPERS =====
    
    public string GetSBVIcon_72() => GetCachedIcon(IconType.SBV_ICON, IconSize.Size_72);
    public string GetSBVIcon_96() => GetCachedIcon(IconType.SBV_ICON, IconSize.Size_96);
    public string GetSBVIcon_128() => GetCachedIcon(IconType.SBV_ICON, IconSize.Size_128);
    public string GetSBVIcon_144() => GetCachedIcon(IconType.SBV_ICON, IconSize.Size_144);
    public string GetSBVIcon_152() => GetCachedIcon(IconType.SBV_ICON, IconSize.Size_152);
    public string GetSBVIcon_192() => GetCachedIcon(IconType.SBV_ICON, IconSize.Size_192);
    public string GetSBVIcon_384() => GetCachedIcon(IconType.SBV_ICON, IconSize.Size_384);
    public string GetSBVIcon_512() => GetCachedIcon(IconType.SBV_ICON, IconSize.Size_512);
    public string GetSBVIcon_1024() => GetCachedIcon(IconType.SBV_ICON, IconSize.Size_1024);
    
    private string GetCachedIcon(IconType iconType, IconSize size)
    {
        var cacheKey = $"{VisualElementsConstants.ICON_CACHE_PREFIX}{iconType}_{(int)size}";
        return _iconCache.TryGetValue(cacheKey, out var url) ? url : VisualElementsConstants.FALLBACK_ICON;
    }

    // ===== SVG METHODS =====
    
    public async Task<string> GetSvgAsync(SvgType svgType, bool useFallback = true)
    {
        if (svgType == SvgType.None)
        {
            return useFallback ? VisualElementsConstants.FALLBACK_SVG : string.Empty;
        }
        
        var cacheKey = $"{VisualElementsConstants.SVG_CACHE_PREFIX}{svgType}";
        
        // Check cache first
        if (_svgCache.TryGetValue(cacheKey, out var cachedSvg))
        {
            return cachedSvg;
        }
        
        var svgPath = VisualElementsConstants.GetSvgPath(svgType);
        
        try
        {
            var svgContent = await _httpClient.GetStringAsync(svgPath);
            
            if (!string.IsNullOrEmpty(svgContent))
            {
                _svgCache.TryAdd(cacheKey, svgContent);
                return svgContent;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"❌ Failed to load SVG {svgPath}: {ex.Message}");
        }
        
        return useFallback ? VisualElementsConstants.FALLBACK_SVG : string.Empty;
    }
    
    public async Task<string> GetSvgAsync(SvgType svgType, int width, int height, bool useFallback = true)
    {
        var svg = await GetSvgAsync(svgType, useFallback);
        return ResizeSvg(svg, width, height);
    }
    
    public async Task<string> GetSvgAsync(SvgType svgType, SvgSize size, bool useFallback = true)
    {
        var dimension = (int)size;
        return await GetSvgAsync(svgType, dimension, dimension, useFallback);
    }
    
    public async Task<string> GetSvgWithColorAsync(SvgType svgType, string color, bool useFallback = true)
    {
        var svg = await GetSvgAsync(svgType, useFallback);
        return ChangeSvgColor(svg, color);
    }
    
    public async Task<string> GetSvgWithColorAsync(SvgType svgType, int width, int height, string color, bool useFallback = true)
    {
        var svg = await GetSvgAsync(svgType, useFallback);
        svg = ResizeSvg(svg, width, height);
        return ChangeSvgColor(svg, color);
    }
    
    public async Task<string> GetCustomSvgAsync(
        SvgType svgType,
        int? width = null,
        int? height = null,
        string? fillColor = null,
        string? strokeColor = null,
        string? className = null,
        string? transform = null,
        bool useFallback = true)
    {
        var svg = await GetSvgAsync(svgType, useFallback);
        
        if (width.HasValue && height.HasValue)
        {
            svg = ResizeSvg(svg, width.Value, height.Value);
        }
        
        if (!string.IsNullOrEmpty(fillColor))
        {
            svg = ChangeSvgColor(svg, fillColor);
        }
        
        if (!string.IsNullOrEmpty(strokeColor))
        {
            svg = ChangeSvgStroke(svg, strokeColor);
        }
        
        if (!string.IsNullOrEmpty(className))
        {
            svg = AddSvgClass(svg, className);
        }
        
        if (!string.IsNullOrEmpty(transform))
        {
            svg = TransformSvg(svg, transform);
        }
        
        return svg;
    }
    
    public async Task<string> GetSvgByNameAsync(string name, bool useFallback = true)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return useFallback ? VisualElementsConstants.FALLBACK_SVG : string.Empty;
        }
        
        var cacheKey = $"{VisualElementsConstants.SVG_CACHE_PREFIX}custom_{name.ToLowerInvariant()}";
        
        if (_svgCache.TryGetValue(cacheKey, out var cachedSvg))
        {
            return cachedSvg;
        }
        
        var svgPath = $"{VisualElementsConstants.SVGS_BASE_PATH}/{name}.svg";
        
        try
        {
            var svgContent = await _httpClient.GetStringAsync(svgPath);
            
            if (!string.IsNullOrEmpty(svgContent))
            {
                _svgCache.TryAdd(cacheKey, svgContent);
                return svgContent;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"❌ Failed to load custom SVG {svgPath}: {ex.Message}");
        }
        
        return useFallback ? VisualElementsConstants.FALLBACK_SVG : string.Empty;
    }
    
    public async Task<bool> SvgExistsAsync(SvgType svgType)
    {
        if (svgType == SvgType.None)
            return false;
        
        try
        {
            var svgPath = VisualElementsConstants.GetSvgPath(svgType);
            var response = await _httpClient.GetAsync(svgPath, HttpCompletionOption.ResponseHeadersRead);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ===== SVG MANIPULATION =====
    
    public string ChangeSvgColor(string svgMarkup, string color)
    {
        if (string.IsNullOrWhiteSpace(svgMarkup))
            return svgMarkup;
        
        // Replace fill attributes
        svgMarkup = Regex.Replace(svgMarkup, @"fill\s*=\s*[""']#[0-9a-fA-F]{3,8}[""']", $"fill=\"{color}\"", RegexOptions.IgnoreCase);
        svgMarkup = Regex.Replace(svgMarkup, @"fill\s*=\s*[""']rgb\([^)]+\)[""']", $"fill=\"{color}\"", RegexOptions.IgnoreCase);
        svgMarkup = Regex.Replace(svgMarkup, @"fill\s*=\s*[""'][^""']+[""']", $"fill=\"{color}\"", RegexOptions.IgnoreCase);
        
        return svgMarkup;
    }
    
    public string ChangeSvgStroke(string svgMarkup, string strokeColor, int? strokeWidth = null)
    {
        if (string.IsNullOrWhiteSpace(svgMarkup))
            return svgMarkup;
        
        // Replace stroke color
        svgMarkup = Regex.Replace(svgMarkup, @"stroke\s*=\s*[""'][^""']+[""']", $"stroke=\"{strokeColor}\"", RegexOptions.IgnoreCase);
        
        // Replace stroke width if provided
        if (strokeWidth.HasValue)
        {
            svgMarkup = Regex.Replace(svgMarkup, @"stroke-width\s*=\s*[""'][^""']+[""']", $"stroke-width=\"{strokeWidth.Value}\"", RegexOptions.IgnoreCase);
        }
        
        return svgMarkup;
    }
    
    public string ResizeSvg(string svgMarkup, int width, int height)
    {
        if (string.IsNullOrWhiteSpace(svgMarkup))
            return svgMarkup;
        
        // Replace or add width attribute
        if (Regex.IsMatch(svgMarkup, @"width\s*=", RegexOptions.IgnoreCase))
        {
            svgMarkup = Regex.Replace(svgMarkup, @"width\s*=\s*[""'][^""']+[""']", $"width=\"{width}\"", RegexOptions.IgnoreCase);
        }
        else
        {
            svgMarkup = Regex.Replace(svgMarkup, @"<svg", $"<svg width=\"{width}\"", RegexOptions.IgnoreCase);
        }
        
        // Replace or add height attribute
        if (Regex.IsMatch(svgMarkup, @"height\s*=", RegexOptions.IgnoreCase))
        {
            svgMarkup = Regex.Replace(svgMarkup, @"height\s*=\s*[""'][^""']+[""']", $"height=\"{height}\"", RegexOptions.IgnoreCase);
        }
        else
        {
            svgMarkup = Regex.Replace(svgMarkup, @"<svg", $"<svg height=\"{height}\"", RegexOptions.IgnoreCase);
        }
        
        return svgMarkup;
    }
    
    public string AddSvgClass(string svgMarkup, string className)
    {
        if (string.IsNullOrWhiteSpace(svgMarkup) || string.IsNullOrWhiteSpace(className))
            return svgMarkup;
        
        if (Regex.IsMatch(svgMarkup, @"class\s*=", RegexOptions.IgnoreCase))
        {
            // Append to existing class
            svgMarkup = Regex.Replace(svgMarkup, @"class\s*=\s*[""']([^""']*)[""']", 
                m => $"class=\"{m.Groups[1].Value} {className}\"", RegexOptions.IgnoreCase);
        }
        else
        {
            // Add new class attribute
            svgMarkup = Regex.Replace(svgMarkup, @"<svg", $"<svg class=\"{className}\"", RegexOptions.IgnoreCase);
        }
        
        return svgMarkup;
    }
    
    public string TransformSvg(string svgMarkup, string transform)
    {
        if (string.IsNullOrWhiteSpace(svgMarkup) || string.IsNullOrWhiteSpace(transform))
            return svgMarkup;
        
        if (Regex.IsMatch(svgMarkup, @"transform\s*=", RegexOptions.IgnoreCase))
        {
            svgMarkup = Regex.Replace(svgMarkup, @"transform\s*=\s*[""'][^""']*[""']", 
                $"transform=\"{transform}\"", RegexOptions.IgnoreCase);
        }
        else
        {
            svgMarkup = Regex.Replace(svgMarkup, @"<svg", $"<svg transform=\"{transform}\"", RegexOptions.IgnoreCase);
        }
        
        return svgMarkup;
    }

    // ===== UTILITY METHODS =====
    
    public string GenerateSvg(string svgMarkup, int width = 24, int height = 24, string? viewBox = null, string? additionalAttributes = null)
    {
        if (string.IsNullOrWhiteSpace(svgMarkup))
        {
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
            return VisualElementsConstants.FALLBACK_ICON;
        }
        
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
    
    public async Task<string> GetSvgAsBase64Async(SvgType svgType)
    {
        var svg = await GetSvgAsync(svgType);
        var bytes = Encoding.UTF8.GetBytes(svg);
        var base64 = Convert.ToBase64String(bytes);
        return $"data:image/svg+xml;base64,{base64}";
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
        
        _logger.LogInformation($"✓ Preloaded {tasks.Count} assets ({_iconCache.Count} icons, {_svgCache.Count} SVGs)");
    }
    
    public async Task PreloadSvgsAsync(params SvgType[] svgTypes)
    {
        var tasks = svgTypes.Select(svgType => GetSvgAsync(svgType)).ToList();
        await Task.WhenAll(tasks);
        
        _logger.LogInformation($"✓ Preloaded {svgTypes.Length} specific SVGs");
    }
    
    public (int IconsCached, int SvgsCached, int TotalCached) GetCacheStats()
    {
        var iconsCached = _iconCache.Count;
        var svgsCached = _svgCache.Count;
        var totalCached = iconsCached + svgsCached;
        
        return (iconsCached, svgsCached, totalCached);
    }
}
