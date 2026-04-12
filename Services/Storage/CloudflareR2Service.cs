// Services/Storage/CloudflareR2Service.cs
using System.Net.Http.Headers;
using System.Net.Http.Json;          // ← FIX: JsonContent lives here
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Storage;

/// <summary>
/// Implementation of ICloudflareR2Service.
///
/// All requests go through the R2 proxy Cloudflare Worker which:
///   1. Validates the Supabase JWT (Authorization: Bearer {token})
///   2. Checks the caller has permission for the target object key
///   3. Proxies the request to R2 (or generates a presigned URL)
///
/// The Worker URL is configured in appsettings.json:
///   "CloudflareR2": { "WorkerBaseUrl": "https://r2-proxy.mysubasha.com" }
///
/// No R2 API key is ever exposed to the browser. The Worker holds
/// the R2 credentials as Worker secrets (set via wrangler CLI).
/// </summary>
public class CloudflareR2Service : ICloudflareR2Service
{
    private readonly HttpClient _http;
    private readonly ISupabaseAuthService _auth;
    private readonly ILogger<CloudflareR2Service> _logger;
    private readonly string _workerBaseUrl;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CloudflareR2Service(
        HttpClient http,
        ISupabaseAuthService auth,
        IConfiguration config,
        ILogger<CloudflareR2Service> logger)
    {
        _http          = http;
        _auth          = auth;
        _logger        = logger;
        _workerBaseUrl = (config["CloudflareR2:WorkerBaseUrl"]
            ?? throw new InvalidOperationException(
                "CloudflareR2:WorkerBaseUrl is not configured in appsettings.json"))
            .TrimEnd('/');
    }

    // ── Presigned Upload ───────────────────────────────────────

    public async Task<R2PresignedUrlResult?> GetPresignedUploadUrlAsync(
        string objectKey,
        string contentType,
        long maxFileSizeBytes = 5_242_880)
    {
        try
        {
            var token = await GetAuthTokenAsync();
            if (token == null)
                return new R2PresignedUrlResult
                {
                    Success = false,
                    ErrorMessage = "Not authenticated"
                };

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_workerBaseUrl}/presign");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            request.Content = JsonContent.Create(new
            {
                object_key   = objectKey,
                content_type = contentType,
                max_size     = maxFileSizeBytes
            });

            var response = await _http.SendAsync(request);
            var raw      = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"R2 presign failed ({response.StatusCode}): {raw}",
                    LogLevel.Error);

                return new R2PresignedUrlResult
                {
                    Success      = false,
                    ErrorMessage = $"Worker error: {response.StatusCode}"
                };
            }

            var result = JsonSerializer.Deserialize<WorkerPresignResponse>(raw, _jsonOptions);

            if (result == null || string.IsNullOrEmpty(result.PresignedUrl))
                return new R2PresignedUrlResult
                {
                    Success      = false,
                    ErrorMessage = "Empty presign response from Worker"
                };

            return new R2PresignedUrlResult
            {
                Success          = true,
                PresignedUrl     = result.PresignedUrl,
                ObjectKey        = objectKey,
                ExpiresInSeconds = result.ExpiresIn
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "R2 GetPresignedUploadUrl");
            return new R2PresignedUrlResult
            {
                Success      = false,
                ErrorMessage = ex.Message
            };
        }
    }

    // ── Direct Upload ──────────────────────────────────────────

    public async Task<R2UploadResult> UploadFileAsync(
        IBrowserFile file,
        string objectKey,
        string contentType,
        long maxFileSizeBytes = 5_242_880)
    {
        try
        {
            var validation = ValidateImageFile(file, maxFileSizeBytes);
            if (!validation.IsValid)
                return new R2UploadResult
                {
                    Success      = false,
                    ErrorMessage = string.Join("; ", validation.Errors)
                };

            await using var stream = file.OpenReadStream(maxFileSizeBytes);
            var bytes = new byte[file.Size];
            var read  = await stream.ReadAsync(bytes, 0, (int)file.Size);

            if (read != file.Size)
                return new R2UploadResult
                {
                    Success      = false,
                    ErrorMessage = "Failed to read file completely"
                };

            return await UploadBytesAsync(bytes, objectKey, contentType);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "R2 UploadFile");
            return new R2UploadResult
            {
                Success      = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<R2UploadResult> UploadBytesAsync(
        byte[] bytes,
        string objectKey,
        string contentType)
    {
        try
        {
            var token = await GetAuthTokenAsync();
            if (token == null)
                return new R2UploadResult
                {
                    Success      = false,
                    ErrorMessage = "Not authenticated"
                };

            var request = new HttpRequestMessage(
                HttpMethod.Put,
                $"{_workerBaseUrl}/upload/{Uri.EscapeDataString(objectKey)}");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            request.Content = new ByteArrayContent(bytes);
            request.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

            var response = await _http.SendAsync(request);
            var raw      = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"R2 upload failed ({response.StatusCode}): {raw}",
                    LogLevel.Error);

                return new R2UploadResult
                {
                    Success      = false,
                    ErrorMessage = $"Upload failed: {response.StatusCode}"
                };
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"R2 upload success: {objectKey} ({bytes.Length} bytes)",
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
            await MID_HelperFunctions.LogExceptionAsync(ex, "R2 UploadBytes");
            return new R2UploadResult
            {
                Success      = false,
                ErrorMessage = ex.Message
            };
        }
    }

    // ── Delete ─────────────────────────────────────────────────

    public async Task<bool> DeleteFileAsync(string objectKey)
    {
        try
        {
            var token = await GetAuthTokenAsync();
            if (token == null) return false;

            var request = new HttpRequestMessage(
                HttpMethod.Delete,
                $"{_workerBaseUrl}/delete/{Uri.EscapeDataString(objectKey)}");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var raw = await response.Content.ReadAsStringAsync();
                await MID_HelperFunctions.DebugMessageAsync(
                    $"R2 delete failed ({response.StatusCode}): {raw}",
                    LogLevel.Error);
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"R2 deleted: {objectKey}", LogLevel.Info);

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "R2 DeleteFile");
            return false;
        }
    }

    public async Task<bool> DeleteFolderAsync(string prefix)
    {
        try
        {
            var objects = await ListObjectsAsync(prefix);

            if (!objects.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"R2 DeleteFolder: no objects found under {prefix}",
                    LogLevel.Info);
                return true;
            }

            var allDeleted = true;

            foreach (var obj in objects)
            {
                var deleted = await DeleteFileAsync(obj.Key);
                if (!deleted)
                {
                    allDeleted = false;
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"R2 DeleteFolder: failed to delete {obj.Key}",
                        LogLevel.Warning);
                }
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"R2 DeleteFolder: deleted {objects.Count} object(s) under {prefix}",
                LogLevel.Info);

            return allDeleted;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "R2 DeleteFolder");
            return false;
        }
    }

    // ── Public URL ─────────────────────────────────────────────

    public string GetPublicUrl(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            return string.Empty;

        return $"{_workerBaseUrl}/{objectKey.TrimStart('/')}";
    }

    public string? ExtractObjectKey(string publicUrl)
    {
        if (string.IsNullOrWhiteSpace(publicUrl))
            return null;

        if (!publicUrl.StartsWith(_workerBaseUrl, StringComparison.OrdinalIgnoreCase))
            return null;

        var key = publicUrl[_workerBaseUrl.Length..].TrimStart('/');
        return string.IsNullOrEmpty(key) ? null : key;
    }

    // ── Object Key Builders ────────────────────────────────────

    public string BuildStoreLogoKey(string partnerId, string fileExtension) =>
        $"partners/{partnerId}/store/logo.{fileExtension.TrimStart('.')}";

    public string BuildStoreBannerKey(string partnerId, string fileExtension) =>
        $"partners/{partnerId}/store/banner.{fileExtension.TrimStart('.')}";

    public string BuildTemplateImageKey(
        string partnerId,
        string templateId,
        string fileName) =>
        $"partners/{partnerId}/templates/{templateId}/{SanitizeFileName(fileName)}";

    public string BuildUserAvatarKey(string userId, string fileExtension) =>
        $"users/{userId}/avatar.{fileExtension.TrimStart('.')}";

    // ── Validation ─────────────────────────────────────────────

    public R2ValidationResult ValidateImageFile(
        IBrowserFile file,
        long maxBytes = 5_242_880)
    {
        var errors = new List<string>();

        if (file == null)
        {
            errors.Add("No file provided");
            return R2ValidationResult.Fail(errors.ToArray());
        }

        if (!R2AllowedContentTypes.IsAllowedImage(file.ContentType))
            errors.Add(
                $"File type '{file.ContentType}' is not allowed. " +
                $"Accepted types: JPEG, PNG, WebP, GIF");

        if (file.Size > maxBytes)
            errors.Add(
                $"File size {file.Size / 1_048_576.0:F1} MB exceeds " +
                $"the maximum of {maxBytes / 1_048_576.0:F0} MB");

        if (file.Size == 0)
            errors.Add("File is empty");

        return errors.Count == 0
            ? R2ValidationResult.Ok()
            : new R2ValidationResult { IsValid = false, Errors = errors };
    }

    // ── List ───────────────────────────────────────────────────

    public async Task<List<R2ObjectInfo>> ListObjectsAsync(
        string prefix = "",
        int maxKeys = 1000)
    {
        try
        {
            var token = await GetAuthTokenAsync();
            if (token == null) return new List<R2ObjectInfo>();

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
                    $"R2 list failed ({response.StatusCode}): {raw}",
                    LogLevel.Error);
                return new List<R2ObjectInfo>();
            }

            var json    = await response.Content.ReadAsStringAsync();
            var objects = JsonSerializer.Deserialize<WorkerListResponse>(json, _jsonOptions);

            if (objects?.Objects == null) return new List<R2ObjectInfo>();

            return objects.Objects.Select(o => new R2ObjectInfo
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
            await MID_HelperFunctions.LogExceptionAsync(ex, "R2 ListObjects");
            return new List<R2ObjectInfo>();
        }
    }

    // ── Private Helpers ────────────────────────────────────────

    private async Task<string?> GetAuthTokenAsync()
    {
        try
        {
            var session = await _auth.GetCurrentSessionAsync();
            return session?.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get auth token for R2 request");
            return null;
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var sanitized = new string(fileName
            .Where(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_')
            .ToArray());

        return string.IsNullOrEmpty(sanitized)
            ? $"file_{Guid.NewGuid():N}"
            : sanitized;
    }

    // ── Worker Response Shapes ─────────────────────────────────

    private class WorkerPresignResponse
    {
        public string? PresignedUrl { get; set; }
        public int ExpiresIn { get; set; } = 300;
    }

    private class WorkerListResponse
    {
        public List<WorkerObjectEntry>? Objects { get; set; }
        public bool Truncated { get; set; }
    }

    private class WorkerObjectEntry
    {
        public string Key { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string? ContentType { get; set; }
    }
}
