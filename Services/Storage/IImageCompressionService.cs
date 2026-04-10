// Services/Storage/IImageCompressionService.cs
using Microsoft.AspNetCore.Components.Forms;

namespace SubashaVentures.Services.Storage;

/// <summary>
/// Service for image compression and optimization using native Blazor APIs.
/// WebP is the default output format — 25–34 % smaller than JPEG at equivalent quality,
/// with 97 % browser market share as of 2024.
/// </summary>
public interface IImageCompressionService
{
    /// <summary>
    /// Compress (and optionally convert to WebP) using Blazor's RequestImageFileAsync.
    /// This is the recommended upload path for IBrowserFile instances.
    /// </summary>
    Task<ImageCompressionResult> CompressImageAsync(
        IBrowserFile browserFile,
        int maxWidth = 2000,
        int maxHeight = 2000,
        int quality = 85,
        bool enableCompression = true,
        ImageOutputFormat outputFormat = ImageOutputFormat.WebP);

    /// <summary>
    /// Compress from stream (fallback — less reliable than the IBrowserFile overload).
    /// </summary>
    Task<ImageCompressionResult> CompressImageFromStreamAsync(
        Stream imageStream,
        string fileName,
        int maxWidth = 2000,
        int maxHeight = 2000,
        bool enableCompression = true,
        ImageOutputFormat outputFormat = ImageOutputFormat.WebP);

    /// <summary>
    /// Validate image format and size before attempting compression/upload.
    /// </summary>
    Task<ImageValidationResult> ValidateImageAsync(
        IBrowserFile browserFile,
        long maxSizeBytes = 50L * 1024L * 1024L);

    /// <summary>
    /// Get the image dimensions. NOTE: exact pixel dimensions are not exposed by the
    /// Blazor IBrowserFile API; this returns null for WASM projects.
    /// </summary>
    Task<ImageDimensions?> GetImageDimensionsAsync(IBrowserFile browserFile);
}

/// <summary>
/// Desired output format for compression.
/// </summary>
public enum ImageOutputFormat
{
    /// <summary>Use WebP when the browser supports it, fall back to JPEG otherwise.</summary>
    WebP,

    /// <summary>Always output JPEG (maximum compatibility).</summary>
    Jpeg,

    /// <summary>Always output PNG (lossless; larger files — avoid for photos).</summary>
    Png,

    /// <summary>Keep the original format unchanged.</summary>
    Original
}

// ─── Result / helper models ───────────────────────────────────────────────────

public class ImageCompressionResult
{
    public bool Success { get; set; }
    public Stream? CompressedStream { get; set; }
    public long OriginalSize { get; set; }
    public long CompressedSize { get; set; }
    public float CompressionRatio { get; set; }
    public string? ErrorMessage { get; set; }
    public string ContentType { get; set; } = "image/webp";
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>True when the output is WebP (the preferred ecommerce format).</summary>
    public bool IsWebP => ContentType == "image/webp";
}

public class ImageDimensions
{
    public int Width { get; set; }
    public int Height { get; set; }
    public float AspectRatio => Width / (float)Height;
}

public class ImageValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Format { get; set; }
    public long FileSize { get; set; }
}
