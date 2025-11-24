// Services/Storage/ImageCacheService.cs
using Microsoft.JSInterop;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Storage;

public class ImageCacheService : IImageCacheService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<ImageCacheService> _logger;
    private IJSObjectReference? _module;
    private bool _initialized = false;

    public ImageCacheService(IJSRuntime jsRuntime, ILogger<ImageCacheService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        if (_module == null && !_initialized)
        {
            try
            {
                _module = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                    "import", "./js/imageCacheHelper.js");
                _initialized = true;
                
                await MID_HelperFunctions.DebugMessageAsync(
                    "✓ Image cache module loaded",
                    LogLevel.Info
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load image cache module");
                throw;
            }
        }

        return _module!;
    }

    public async Task<string> GetCachedImageUrlAsync(string imageUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(imageUrl))
                return string.Empty;

            var module = await GetModuleAsync();
            
            // Get cached blob
            var blob = await module.InvokeAsync<IJSObjectReference>("getCachedImage", imageUrl);
            
            // Create object URL for blob
            var objectUrl = await _jsRuntime.InvokeAsync<string>("URL.createObjectURL", blob);
            
            return objectUrl;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache fetch failed for: {Url}, using direct URL", imageUrl);
            // Fallback to original URL
            return imageUrl;
        }
    }

    public async Task PreloadImagesAsync(List<string> imageUrls)
    {
        try
        {
            if (imageUrls == null || !imageUrls.Any())
                return;

            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("preloadImages", imageUrls);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Preloaded {imageUrls.Count} images",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to preload images");
        }
    }

    public async Task<bool> ClearCacheAsync()
    {
        try
        {
            var module = await GetModuleAsync();
            return await module.InvokeAsync<bool>("clearImageCache");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear cache");
            return false;
        }
    }

    public async Task<ImageCacheStats> GetCacheStatsAsync()
    {
        try
        {
            var module = await GetModuleAsync();
            var stats = await module.InvokeAsync<ImageCacheStats>("getCacheSize");
            return stats ?? new ImageCacheStats();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cache stats");
            return new ImageCacheStats();
        }
    }

    public async Task<int> ClearExpiredCacheAsync()
    {
        try
        {
            var module = await GetModuleAsync();
            return await module.InvokeAsync<int>("clearExpiredCache");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear expired cache");
            return 0;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module != null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing image cache module");
            }
        }
    }
}