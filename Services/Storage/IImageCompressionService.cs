// Services/Storage/IImageCompressionService.cs & Implementation
using System.Text;
using SubashaVentures.Utilities.HelperScripts;

namespace SubashaVentures.Services.Storage;

/// <summary>
/// Service for image compression and optimization
/// Uses base64 encoding for WASM-safe operations
/// </summary>
public interface IImageCompressionService
{
    /// <summary>
    /// Compress image from stream with specified quality
    /// </summary>
    Task<ImageCompressionResult> CompressImageAsync(
        Stream imageStream,
        int quality = 80,
        int maxWidth = 2000,
        int maxHeight = 2000,
        bool convertToWebP = false);
    
    /// <summary>
    /// Compress image and return base64 string
    /// </summary>
    Task<string> CompressToBase64Async(
        Stream imageStream,
        int quality = 80,
        int maxWidth = 2000,
        int maxHeight = 2000);
    
    /// <summary>
    /// Create thumbnail from image
    /// </summary>
    Task<ImageCompressionResult> CreateThumbnailAsync(
        Stream imageStream,
        int size = 300,
        int quality = 85);
    
    /// <summary>
    /// Get image dimensions without full decode
    /// </summary>
    Task<ImageDimensions?> GetImageDimensionsAsync(Stream imageStream);
    
    /// <summary>
    /// Convert base64 back to stream
    /// </summary>
    Stream Base64ToStream(string base64String);
    
    /// <summary>
    /// Validate image format and size
    /// </summary>
    ImageValidationResult ValidateImage(
        Stream imageStream,
        long maxSizeBytes = 50 * 1024 * 1024);
    
    async Task<ImageValidationResult> ValidateImageAsync(
        Stream imageStream,
        long maxSizeBytes = 50L * 1024L * 1024L);
}

/// <summary>
/// Image compression result
/// </summary>
public class ImageCompressionResult
{
    public bool Success { get; set; }
    public Stream? CompressedStream { get; set; }
    public string? Base64Data { get; set; }
    public long OriginalSize { get; set; }
    public long CompressedSize { get; set; }
    public float CompressionRatio { get; set; }
    public string? ErrorMessage { get; set; }
    public string ContentType { get; set; } = "image/jpeg";
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
