// Services/Storage/IImageCacheService.cs
using Microsoft.JSInterop;

namespace SubashaVentures.Services.Storage;

public interface IImageCacheService
{
    Task<string> GetCachedImageUrlAsync(string imageUrl);
    Task PreloadImagesAsync(List<string> imageUrls);
    Task<bool> ClearCacheAsync();
    Task<ImageCacheStats> GetCacheStatsAsync();
    Task<int> ClearExpiredCacheAsync();
}

public class ImageCacheStats
{
    public int Count { get; set; }
    public long Size { get; set; }
    public string Formatted { get; set; } = "";
}