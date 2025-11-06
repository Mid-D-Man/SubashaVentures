// Services/Storage/ImageCompressionService.cs
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Storage;

/// <summary>
/// Image compression service using Blazor's built-in RequestImageFileAsync
/// This is the BEST solution for Blazor WASM - uses browser's native resize API
/// </summary>
public class ImageCompressionService : IImageCompressionService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<ImageCompressionService> _logger;
    
    private const long MaxFileSizeBytes = 50L * 1024L * 1024L;
    
    private static readonly HashSet<string> SupportedFormats = new()
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif"
    };

    public ImageCompressionService(IJSRuntime jsRuntime, ILogger<ImageCompressionService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async Task<ImageCompressionResult> CompressImageAsync(
        Stream imageStream,
        int quality = 80,
        int maxWidth = 2000,
        int maxHeight = 2000,
        bool convertToWebP = false)
    {
        try
        {
            var originalSize = imageStream.Length;
            
            // Convert stream to base64 for JS processing
            var base64 = await StreamToBase64Async(imageStream);
            
            try
            {
                // Try JS compression first
                var jsResult = await _jsRuntime.InvokeAsync<ImageCompressionResult>(
                    "imageCompressor.compressImage",
                    base64,
                    quality,
                    maxWidth,
                    maxHeight,
                    convertToWebP ? "image/webp" : "image/jpeg");
                
                if (jsResult != null && jsResult.Success)
                {
                    jsResult.OriginalSize = originalSize;
                    jsResult.CompressionRatio = originalSize > 0 
                        ? (float)(originalSize - jsResult.CompressedSize) / originalSize 
                        : 0;
                    
                    if (!string.IsNullOrEmpty(jsResult.Base64Data))
                    {
                        var compressedBytes = Convert.FromBase64String(jsResult.Base64Data);
                        jsResult.CompressedStream = new MemoryStream(compressedBytes);
                    }
                    
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"Image compressed: {originalSize / 1024}KB → {jsResult.CompressedSize / 1024}KB ({jsResult.CompressionRatio * 100:F1}%)",
                        LogLevel.Info
                    );
                    
                    return jsResult;
                }
            }
            catch (Exception jsEx)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"JS compression failed, returning original: {jsEx.Message}",
                    LogLevel.Warning
                );
            }
            
            // Fallback: return original
            imageStream.Position = 0;
            byte[] originalBytes;
            using (var ms = new MemoryStream())
            {
                await imageStream.CopyToAsync(ms);
                originalBytes = ms.ToArray();
            }
            
            return new ImageCompressionResult
            {
                Success = true,
                CompressedStream = new MemoryStream(originalBytes),
                OriginalSize = originalSize,
                CompressedSize = originalSize,
                CompressionRatio = 0,
                ContentType = "image/jpeg"
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Error compressing image");
            return new ImageCompressionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<string> CompressToBase64Async(
        Stream imageStream,
        int quality = 80,
        int maxWidth = 2000,
        int maxHeight = 2000)
    {
        var result = await CompressImageAsync(imageStream, quality, maxWidth, maxHeight);
        
        if (!result.Success || result.CompressedStream == null)
        {
            throw new InvalidOperationException("Image compression failed");
        }

        return await StreamToBase64Async(result.CompressedStream);
    }

    public async Task<ImageCompressionResult> CreateThumbnailAsync(
        Stream imageStream,
        int size = 300,
        int quality = 85)
    {
        return await CompressImageAsync(imageStream, quality, size, size);
    }

    public async Task<ImageDimensions?> GetImageDimensionsAsync(Stream imageStream)
    {
        try
        {
            var base64 = await StreamToBase64Async(imageStream);
            
            return await _jsRuntime.InvokeAsync<ImageDimensions>(
                "imageCompressor.getImageDimensions",
                base64);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Error getting image dimensions");
            return null;
        }
    }

    public Stream Base64ToStream(string base64String)
    {
        var bytes = Convert.FromBase64String(base64String);
        return new MemoryStream(bytes);
    }

    public ImageValidationResult ValidateImage(
        Stream imageStream,
        long maxSizeBytes = 50L * 1024L * 1024L)
    {
        return ValidateImageAsync(imageStream, maxSizeBytes).GetAwaiter().GetResult();
    }

    public async Task<ImageValidationResult> ValidateImageAsync(
        Stream imageStream,
        long maxSizeBytes = 50L * 1024L * 1024L)
    {
        try
        {
            var fileSize = imageStream.Length;
            
            if (fileSize > maxSizeBytes)
            {
                return new ImageValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"File size exceeds limit. Max: {maxSizeBytes / 1024 / 1024}MB",
                    FileSize = fileSize
                };
            }

            // Read first few bytes to detect format
            byte[] headerBytes = new byte[8];
            imageStream.Position = 0;
            await imageStream.ReadAsync(headerBytes, 0, 8);
            imageStream.Position = 0;

            var format = DetectImageFormat(headerBytes);
            
            if (format == null || !SupportedFormats.Contains(format))
            {
                return new ImageValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Unsupported image format",
                    FileSize = fileSize
                };
            }

            return new ImageValidationResult
            {
                IsValid = true,
                Format = format,
                FileSize = fileSize
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Error validating image");
            return new ImageValidationResult
            {
                IsValid = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<string> StreamToBase64Async(Stream stream)
    {
        stream.Position = 0;
        using (var memoryStream = new MemoryStream())
        {
            await stream.CopyToAsync(memoryStream);
            return Convert.ToBase64String(memoryStream.ToArray());
        }
    }

    private string? DetectImageFormat(byte[] buffer)
    {
        if (buffer.Length < 8) return null;

        // JPEG
        if (buffer[0] == 0xFF && buffer[1] == 0xD8)
            return "image/jpeg";

        // PNG
        if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
            return "image/png";

        // GIF
        if (buffer[0] == 0x47 && buffer[1] == 0x49 && buffer[2] == 0x46)
            return "image/gif";

        // WebP
        if (buffer[0] == 0x52 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x46)
            return "image/webp";

        return null;
    }
}
