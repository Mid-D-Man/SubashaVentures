// Services/Storage/IImageCompressionService.cs
using Microsoft.AspNetCore.Components.Forms;

namespace SubashaVentures.Services.Storage;

/// <summary>
/// Service for image compression and optimization using native Blazor APIs
/// </summary>
public interface IImageCompressionService
{
    /// <summary>
    /// Compress image using Blazor's built-in RequestImageFileAsync
    /// This uses native browser APIs and is the recommended approach
    /// </summary>
    Task<ImageCompressionResult> CompressImageAsync(
        IBrowserFile browserFile,
        int maxWidth = 2000,
        int maxHeight = 2000,
        int quality = 80,
        bool enableCompression = true);
    
    /// <summary>
    /// Compress image from stream (fallback - less reliable)
    /// </summary>
    Task<ImageCompressionResult> CompressImageFromStreamAsync(
        Stream imageStream,
        string fileName,
        int maxWidth = 2000,
        int maxHeight = 2000,
        bool enableCompression = true);
    
    /// <summary>
    /// Validate image format and size
    /// </summary>
    Task<ImageValidationResult> ValidateImageAsync(
        IBrowserFile browserFile,
        long maxSizeBytes = 50L * 1024L * 1024L);
    
    /// <summary>
    /// Get image dimensions
    /// </summary>
    Task<ImageDimensions?> GetImageDimensionsAsync(IBrowserFile browserFile);
}

/// <summary>
/// Image compression result
/// </summary>
public class ImageCompressionResult
{
    public bool Success { get; set; }
    public Stream? CompressedStream { get; set; }
    public long OriginalSize { get; set; }
    public long CompressedSize { get; set; }
    public float CompressionRatio { get; set; }
    public string? ErrorMessage { get; set; }
    public string ContentType { get; set; } = "image/jpeg";
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>
/// Image dimensions
/// </summary>
public class ImageDimensions
{
    public int Width { get; set; }
    public int Height { get; set; }
    public float AspectRatio => Width / (float)Height;
}

/// <summary>
/// Image validation result
/// </summary>
public class ImageValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Format { get; set; }
    public long FileSize { get; set; }
}