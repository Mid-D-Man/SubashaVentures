using SubashaVentures.Domain.Order;

namespace SubashaVentures.Services.Collection;

/// <summary>
/// High-level service for store-collection (pickup) flows.
/// Wraps the two edge functions and handles QR URL parsing.
/// </summary>
public interface ICollectionService
{
    /// <summary>
    /// Generates (or retrieves existing) signed QR URL for an order.
    /// Called from OrderDetails for pickup orders.
    /// </summary>
    Task<string?> GetCollectionQrUrlAsync(string orderId);

    /// <summary>
    /// Parses a scanned URL, calls validate-collection-token, returns receipt.
    /// Admin scanner calls this.
    /// </summary>
    Task<CollectionValidationResult> ValidateScannedUrlAsync(string scannedUrl);
}
