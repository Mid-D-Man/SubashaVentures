// Services/Storage/CloudflareR2Service.cs
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Storage;

/// <summary>
/// Cloudflare R2 upload service.
/// Every image upload is automatically:
///   1. Validated (type + size)
///   2. Compressed and converted to WebP via ImageCompressionService
///   3. Renamed to a clean, predictable key
///   4. Uploaded to R2 via the proxy Worker
/// </summary>
public class CloudflareR2Service : ICloudflareR2Service
{
    private readonly HttpClient               _http;
    private readonly ISupabaseAuthService     _auth;
    private readonly IImageCompressionService _compression;
    private readonly ILogger<CloudflareR2Service> _logger;
    private readonly string _workerBaseUrl;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CloudflareR2Service(
        HttpClient               http,
        ISupabaseAuthService     auth,
        IImageCompressionService compression,
        IConfiguration           config,
        ILogger<CloudflareR2Service> logger)
    {
        _http        = http;
        _auth        = auth;
        _compression = compression;
        _logger      = logger;

        _workerBaseUrl = (config["CloudflareR2:WorkerBaseUrl"]
            ?? throw new InvalidOperationException(
                "CloudflareR2:WorkerBaseUrl missing from appsettings.json"))
            .TrimEnd('/');
    }

    // ── Upload ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Upload a browser file to R2.
    /// Automatically compresses and converts to WebP before upload.
    /// </summary>
    public async Task<R2UploadResult> UploadImageAsync(
        IBrowserFile file,
        string objectKey,
        long maxFileSizeBytes = 5_242_880)
    {
        try
        {
            // 1. Validate
            var validation = ValidateImageFile(file, maxFileSizeBytes);
            if (!validation.IsValid)
                return Fail(string.Join("; ", validation.Errors));

            // 2. Compress + convert to WebP
            await MID_HelperFunctions.DebugMessageAsync(
                $"[R2] Compressing {file.Name} ({file.Size / 1024.0:F0} KB) → WebP",
                LogLevel.Info);

            var compression = await _compression.CompressImageAsync(
                file,
                maxWidth:          2000,
                maxHeight:         2000,
                quality:           85,
                enableCompression: true,
                outputFormat:      ImageOutputFormat.WebP);

            byte[] bytes;
            string finalContentType;

            if (compression.Success && compression.CompressedStream != null)
            {
                using var ms = new MemoryStream();
                await compression.CompressedStream.CopyToAsync(ms);
                bytes           = ms.ToArray();
                finalContentType = compression.ContentType; // image/webp

                await MID_HelperFunctions.DebugMessageAsync(
                    $"[R2] Compressed: {file.Size / 1024.0:F0} KB → {bytes.Length / 1024.0:F0} KB " +
                    $"({compression.CompressionRatio * 100:F1}% saved)",
                    LogLevel.Info);
            }
            else
            {
                // Fallback: use original bytes if compression fails
                _logger.LogWarning("WebP compression failed for {Name}, using original", file.Name);
                await using var stream = file.OpenReadStream(maxFileSizeBytes);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                bytes           = ms.ToArray();
                finalContentType = file.ContentType;
            }

            // 3. Force .webp extension on the key
            var webpKey = EnsureWebpExtension(objectKey, finalContentType);

            // 4. Upload
            return await UploadBytesAsync(bytes, webpKey, finalContentType);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"R2 UploadImageAsync: {file.Name}");
            return Fail(ex.Message);
        }
    }

    /// <summary>Upload raw bytes. Use when you already have processed data.</summary>
    public async Task<R2UploadResult> UploadBytesAsync(
        byte[] bytes,
        string objectKey,
        string contentType = "image/webp")
    {
        try
        {
            var token = await GetAuthTokenAsync();
            if (token == null) return Fail("Not authenticated");

            var encodedKey = string.Join("/",
                objectKey.Split('/').Select(Uri.EscapeDataString));

            var request = new HttpRequestMessage(
                HttpMethod.Put,
                $"{_workerBaseUrl}/upload/{encodedKey}");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            request.Content = new ByteArrayContent(bytes);
            request.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            request.Content.Headers.Add("Content-Length", bytes.Length.ToString());

            var response = await _http.SendAsync(request);
            var raw      = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"[R2] Upload failed ({response.StatusCode}): {raw}", LogLevel.Error);
                return Fail($"Upload failed ({response.StatusCode}): {raw}");
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"[R2] ✓ Uploaded: {objectKey} ({bytes.Length / 1024.0:F1} KB)",
                LogLevel.Info);

            return new R2UploadResult
            {
                Success       = true,
                ObjectKey     = objectKey,
                PublicUrl     = GetPublicUrl(objectKey),
                FileSizeBytes = bytes.Length,
                ContentType   = contentType
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"R2 UploadBytesAsync: {objectKey}");
            return Fail(ex.Message);
        }
    }

    // ── Delete ─────────────────────────────────────────────────────────────────

    public async Task<bool> DeleteFileAsync(string objectKey)
    {
        try
        {
            var token = await GetAuthTokenAsync();
            if (token == null) return false;

            var encodedKey = string.Join("/",
                objectKey.Split('/').Select(Uri.EscapeDataString));

            var request = new HttpRequestMessage(
                HttpMethod.Delete,
                $"{_workerBaseUrl}/delete/{encodedKey}");
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var raw = await response.Content.ReadAsStringAsync();
                await MID_HelperFunctions.DebugMessageAsync(
                    $"[R2] Delete failed ({response.StatusCode}): {raw}", LogLevel.Error);
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"[R2] ✓ Deleted: {objectKey}", LogLevel.Info);
            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"R2 DeleteFileAsync: {objectKey}");
            return false;
        }
    }

    public async Task<bool> DeleteFolderAsync(string prefix)
    {
        try
        {
            var objects = await ListObjectsAsync(prefix);
            if (!objects.Any()) return true;

            var allDeleted = true;
            foreach (var obj in objects)
            {
                if (!await DeleteFileAsync(obj.Key))
                    allDeleted = false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"[R2] Deleted folder: {prefix} ({objects.Count} objects)", LogLevel.Info);
            return allDeleted;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"R2 DeleteFolderAsync: {prefix}");
            return false;
        }
    }

    // ── Public URL ─────────────────────────────────────────────────────────────

    public string GetPublicUrl(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey)) return string.Empty;
        return $"{_workerBaseUrl}/{objectKey.TrimStart('/')}";
    }

    public string? ExtractObjectKey(string publicUrl)
    {
        if (string.IsNullOrWhiteSpace(publicUrl)) return null;
        if (!publicUrl.StartsWith(_workerBaseUrl, StringComparison.OrdinalIgnoreCase))
            return null;
        var key = publicUrl[_workerBaseUrl.Length..].TrimStart('/');
        return string.IsNullOrEmpty(key) ? null : key;
    }

    // ── Key Builders ───────────────────────────────────────────────────────────
    // All keys are always .webp — clean, predictable, overwrite-safe.

    /// <summary>partners/{partnerId}/store/logo.webp</summary>
    public string BuildStoreLogoKey(string partnerId) =>
        $"partners/{partnerId}/store/logo.webp";

    /// <summary>partners/{partnerId}/store/banner.webp</summary>
    public string BuildStoreBannerKey(string partnerId) =>
        $"partners/{partnerId}/store/banner.webp";

    /// <summary>
    /// partners/{partnerId}/templates/{templateId}/img_{index}_{token}.webp
    /// imageIndex is the 0-based position in the template's image list.
    /// </summary>
    public string BuildTemplateImageKey(string partnerId, string templateId, int imageIndex)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        return $"partners/{partnerId}/templates/{templateId}/img_{imageIndex:D2}_{token}.webp";
    }

    /// <summary>users/{userId}/avatar.webp</summary>
    public string BuildUserAvatarKey(string userId) =>
        $"users/{userId}/avatar.webp";

    // ── Validation ─────────────────────────────────────────────────────────────

    public R2ValidationResult ValidateImageFile(IBrowserFile file, long maxBytes = 5_242_880)
    {
        var errors = new List<string>();

        if (file == null)
        {
            errors.Add("No file provided");
            return R2ValidationResult.Fail(errors.ToArray());
        }

        if (!R2AllowedContentTypes.IsAllowedImage(file.ContentType))
            errors.Add(
                $"'{file.ContentType}' is not allowed. " +
                "Accepted: JPEG, PNG, WebP, GIF");

        if (file.Size > maxBytes)
            errors.Add(
                $"File size {file.Size / 1_048_576.0:F1} MB exceeds " +
                $"limit of {maxBytes / 1_048_576.0:F0} MB");

        if (file.Size == 0)
            errors.Add("File is empty");

        return errors.Count == 0
            ? R2ValidationResult.Ok()
            : new R2ValidationResult { IsValid = false, Errors = errors };
    }

    // ── List ───────────────────────────────────────────────────────────────────

    public async Task<List<R2ObjectInfo>> ListObjectsAsync(
        string prefix = "", int maxKeys = 1000)
    {
        try
        {
            var token = await GetAuthTokenAsync();
            if (token == null) return new();

            var url = string.IsNullOrEmpty(prefix)
                ? $"{_workerBaseUrl}/list?max_keys={maxKeys}"
                : $"{_workerBaseUrl}/list?prefix={Uri.EscapeDataString(prefix)}&max_keys={maxKeys}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var raw = await response.Content.ReadAsStringAsync();
                await MID_HelperFunctions.DebugMessageAsync(
                    $"[R2] List failed ({response.StatusCode}): {raw}", LogLevel.Error);
                return new();
            }

            var json    = await response.Content.ReadAsStringAsync();
            var listed  = JsonSerializer.Deserialize<WorkerListResponse>(json, _jsonOptions);

            if (listed?.Objects == null) return new();

            return listed.Objects.Select(o => new R2ObjectInfo
            {
                Key          = o.Key,
                Size         = o.Size,
                LastModified = o.LastModified,
                ContentType  = o.ContentType,
                PublicUrl    = GetPublicUrl(o.Key)
            }).ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "R2 ListObjectsAsync");
            return new();
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task<string?> GetAuthTokenAsync()
    {
        try
        {
            var session = await _auth.GetCurrentSessionAsync();
            return session?.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get auth token for R2");
            return null;
        }
    }

    /// <summary>
    /// If the image was converted to WebP, switch the key extension to .webp.
    /// Otherwise keep the original extension (gif stays gif, etc.).
    /// </summary>
    private static string EnsureWebpExtension(string objectKey, string contentType)
    {
        if (contentType == "image/webp")
        {
            var lastDot = objectKey.LastIndexOf('.');
            return lastDot >= 0
                ? objectKey[..lastDot] + ".webp"
                : objectKey + ".webp";
        }
        return objectKey;
    }

    private static R2UploadResult Fail(string msg) =>
        new() { Success = false, ErrorMessage = msg };

    // ── Worker response shapes ─────────────────────────────────────────────────

    private class WorkerListResponse
    {
        public List<WorkerObject>? Objects   { get; set; }
        public bool                Truncated { get; set; }
    }

    private class WorkerObject
    {
        public string   Key          { get; set; } = string.Empty;
        public long     Size         { get; set; }
        public DateTime LastModified { get; set; }
        public string?  ContentType  { get; set; }
    }
}
