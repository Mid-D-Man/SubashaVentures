using SubashaVentures.Domain.Order;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Collection;

public class CollectionService : ICollectionService
{
    private readonly ISupabaseEdgeFunctionService _edge;
    private readonly ILogger<CollectionService> _logger;

    // Must match APP_URL in edge functions
    private const string AppHost = "mysubasha.com";

    public CollectionService(
        ISupabaseEdgeFunctionService edge,
        ILogger<CollectionService> logger)
    {
        _edge   = edge;
        _logger = logger;
    }

    public async Task<string?> GetCollectionQrUrlAsync(string orderId)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Getting collection QR URL for order: {orderId}", LogLevel.Info);

            var url = await _edge.GenerateCollectionTokenAsync(orderId);

            if (string.IsNullOrEmpty(url))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "generate-collection-token returned null", LogLevel.Warning);
                return null;
            }

            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetCollectionQrUrlAsync failed for order {OrderId}", orderId);
            return null;
        }
    }

    public async Task<CollectionValidationResult> ValidateScannedUrlAsync(string scannedUrl)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Validating scanned URL: {scannedUrl[..Math.Min(scannedUrl.Length, 60)]}...",
                LogLevel.Info);

            // Reject anything not from our domain
            if (!Uri.TryCreate(scannedUrl, UriKind.Absolute, out var uri))
                return Fail("Scanned value is not a valid URL.");

            if (!uri.Host.Equals(AppHost, StringComparison.OrdinalIgnoreCase))
                return Fail($"QR code is not from mysubasha.com (got: {uri.Host})");

            if (!uri.AbsolutePath.Equals("/collect", StringComparison.OrdinalIgnoreCase))
                return Fail("QR code does not point to a valid collection page.");

            // Parse ?t=...&s=...
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var t     = query["t"];
            var s     = query["s"];

            if (string.IsNullOrEmpty(t) || string.IsNullOrEmpty(s))
                return Fail("QR code is missing required parameters.");

            return await _edge.ValidateCollectionTokenAsync(t, s);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ValidateScannedUrlAsync failed");
            return Fail("An unexpected error occurred while validating the QR code.");
        }
    }

    private static CollectionValidationResult Fail(string error)
        => new() { Success = false, Error = error };
}
