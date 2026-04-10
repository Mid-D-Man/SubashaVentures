// Services/Storage/ImageCompressionService.cs
// WebP-first image compression using Blazor's RequestImageFileAsync.
// The browser's Canvas API encodes WebP natively (97 % market share, 25-34 % smaller than JPEG).

using Microsoft.AspNetCore.Components.Forms;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Storage;

public class ImageCompressionService : IImageCompressionService
{
    private readonly ILogger<ImageCompressionService> _logger;

    private const long MaxFileSizeBytes = 50L * 1024L * 1024L;

    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif"
    };

    // RequestImageFileAsync maps to these MIME types:
    private const string MimeWebP = "image/webp";
    private const string MimeJpeg = "image/jpeg";
    private const string MimePng  = "image/png";

    public ImageCompressionService(ILogger<ImageCompressionService> logger)
    {
        _logger = logger;
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    public async Task<ImageCompressionResult> CompressImageAsync(
        IBrowserFile browserFile,
        int maxWidth     = 2000,
        int maxHeight    = 2000,
        int quality      = 85,
        bool enableCompression = true,
        ImageOutputFormat outputFormat = ImageOutputFormat.WebP)
    {
        try
        {
            var originalSize = browserFile.Size;
            var targetMime   = ResolveMime(outputFormat, browserFile.ContentType);

            await MID_HelperFunctions.DebugMessageAsync(
                $"[Compression] {browserFile.Name} ({FormatBytes(originalSize)}) → {targetMime} " +
                $"[compression={enableCompression}]",
                LogLevel.Info);

            if (!enableCompression)
            {
                return new ImageCompressionResult
                {
                    Success          = true,
                    CompressedStream = browserFile.OpenReadStream(MaxFileSizeBytes),
                    OriginalSize     = originalSize,
                    CompressedSize   = originalSize,
                    CompressionRatio = 0f,
                    ContentType      = browserFile.ContentType,
                    ErrorMessage     = "Compression disabled — original file returned"
                };
            }

            // Use Blazor's built-in browser-native resize/compress.
            // Passing 'image/webp' tells the underlying Canvas API to encode as WebP.
            var resized = await browserFile.RequestImageFileAsync(targetMime, maxWidth, maxHeight);

            var compressedSize   = resized.Size;
            var compressedStream = resized.OpenReadStream(MaxFileSizeBytes);

            var ratio = originalSize > 0
                ? Math.Max(0f, (float)(originalSize - compressedSize) / originalSize)
                : 0f;

            await MID_HelperFunctions.DebugMessageAsync(
                $"[Compression] ✓ {FormatBytes(originalSize)} → {FormatBytes(compressedSize)} " +
                $"({ratio * 100:F1}% saved) [{targetMime}]",
                LogLevel.Info);

            return new ImageCompressionResult
            {
                Success          = true,
                CompressedStream = compressedStream,
                OriginalSize     = originalSize,
                CompressedSize   = compressedSize,
                CompressionRatio = ratio,
                ContentType      = resized.ContentType,
                Width            = maxWidth,
                Height           = maxHeight
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"CompressImageAsync: {browserFile.Name}");
            _logger.LogWarning("Compression failed — returning original file as fallback");

            try
            {
                return new ImageCompressionResult
                {
                    Success          = true,
                    CompressedStream = browserFile.OpenReadStream(MaxFileSizeBytes),
                    OriginalSize     = browserFile.Size,
                    CompressedSize   = browserFile.Size,
                    CompressionRatio = 0f,
                    ContentType      = browserFile.ContentType,
                    ErrorMessage     = $"Compression failed (original used): {ex.Message}"
                };
            }
            catch (Exception fallbackEx)
            {
                await MID_HelperFunctions.LogExceptionAsync(fallbackEx, "Fallback stream open failed");
                return new ImageCompressionResult
                {
                    Success      = false,
                    ErrorMessage = $"Complete failure: {fallbackEx.Message}"
                };
            }
        }
    }

    public async Task<ImageCompressionResult> CompressImageFromStreamAsync(
        Stream imageStream,
        string fileName,
        int maxWidth  = 2000,
        int maxHeight = 2000,
        bool enableCompression = true,
        ImageOutputFormat outputFormat = ImageOutputFormat.WebP)
    {
        // Stream-based path cannot invoke RequestImageFileAsync.
        // We return as-is and warn — callers should prefer the IBrowserFile overload.
        await MID_HelperFunctions.DebugMessageAsync(
            "[Compression] WARNING: stream-based path — no conversion performed. " +
            "Use the IBrowserFile overload for WebP conversion.",
            LogLevel.Warning);

        var targetMime = outputFormat == ImageOutputFormat.Original
            ? MimeJpeg
            : ResolveMime(outputFormat, MimeJpeg);

        return new ImageCompressionResult
        {
            Success          = true,
            CompressedStream = imageStream,
            OriginalSize     = imageStream.Length,
            CompressedSize   = imageStream.Length,
            CompressionRatio = 0f,
            ContentType      = targetMime,
            ErrorMessage     = "Stream-based path — no conversion; use IBrowserFile overload for WebP"
        };
    }

    public async Task<ImageValidationResult> ValidateImageAsync(
        IBrowserFile browserFile,
        long maxSizeBytes = 50L * 1024L * 1024L)
    {
        try
        {
            var fileSize = browserFile.Size;

            if (fileSize > maxSizeBytes)
            {
                return new ImageValidationResult
                {
                    IsValid      = false,
                    ErrorMessage = $"File size {FormatBytes(fileSize)} exceeds limit of {FormatBytes(maxSizeBytes)}",
                    FileSize     = fileSize
                };
            }

            var contentType = browserFile.ContentType.ToLowerInvariant();

            if (!SupportedFormats.Contains(contentType))
            {
                return new ImageValidationResult
                {
                    IsValid      = false,
                    ErrorMessage = $"Unsupported format '{contentType}'. Accepted: JPEG, PNG, WebP, GIF",
                    FileSize     = fileSize,
                    Format       = contentType
                };
            }

            return new ImageValidationResult
            {
                IsValid  = true,
                Format   = contentType,
                FileSize = fileSize
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "ValidateImageAsync");
            return new ImageValidationResult { IsValid = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<ImageDimensions?> GetImageDimensionsAsync(IBrowserFile browserFile)
    {
        // Blazor's IBrowserFile API does not expose exact pixel dimensions.
        await MID_HelperFunctions.DebugMessageAsync(
            "[Compression] Exact dimensions unavailable via Blazor IBrowserFile API",
            LogLevel.Warning);
        return null;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolve the target MIME type from the requested <see cref="ImageOutputFormat"/>.
    /// WebP is chosen by default; falls back to JPEG when the format is Original or
    /// when the input is already JPEG.
    /// </summary>
    private static string ResolveMime(ImageOutputFormat format, string originalMime)
    {
        return format switch
        {
            ImageOutputFormat.WebP     => MimeWebP,
            ImageOutputFormat.Jpeg     => MimeJpeg,
            ImageOutputFormat.Png      => MimePng,
            ImageOutputFormat.Original => originalMime,
            _                          => MimeWebP
        };
    }

    /// <summary>
    /// Map a MIME type to the correct file extension (used when naming upload files).
    /// </summary>
    public static string MimeToExtension(string mimeType) => mimeType.ToLowerInvariant() switch
    {
        MimeWebP      => ".webp",
        "image/jpg"   => ".jpg",
        MimeJpeg      => ".jpg",
        MimePng       => ".png",
        "image/gif"   => ".gif",
        _             => ".webp"   // default to WebP for unknown types
    };

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1) { order++; len /= 1024; }
        return $"{len:0.##} {sizes[order]}";
    }
}
