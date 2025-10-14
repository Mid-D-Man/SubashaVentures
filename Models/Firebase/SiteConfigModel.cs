namespace SubashaVentures.Models.Firebase;

/// <summary>
/// Site-wide configuration (cached in Firestore)
/// </summary>
public record SiteConfigModel
{
    public string Id { get; init; } = "site_config";
    public string SiteName { get; init; } = "SubashaVentures";
    public string SiteUrl { get; init; } = string.Empty;
    public string? Logo { get; init; }
    public string? Favicon { get; init; }
    public string Currency { get; init; } = "NGN";
    public decimal FreeShippingThreshold { get; init; } = 50000;
    public decimal StandardShippingCost { get; init; } = 2000;
    public decimal TaxRate { get; init; } = 0;
    public bool MaintenanceMode { get; init; }
    public string? MaintenanceMessage { get; init; }
    public DateTime UpdatedAt { get; init; }
}
