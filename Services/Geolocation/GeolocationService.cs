// Services/Geolocation/GeolocationService.cs - UPDATED WITH GPS PRIORITY
using Microsoft.JSInterop;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Geolocation;

public interface IGeolocationService
{
    Task<AddressComponents?> GetLocationAsync();
    Task<AddressComponents?> GetLocationFromIPAsync();
    Task<GeolocationPosition?> GetPreciseLocationAsync();
}

public class GeolocationService : IGeolocationService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<GeolocationService> _logger;

    public GeolocationService(IJSRuntime jsRuntime, ILogger<GeolocationService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>
    /// Get location - tries GPS first, falls back to IP if GPS fails
    /// </summary>
    public async Task<AddressComponents?> GetLocationAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "🎯 Attempting to get location (GPS preferred)",
                LogLevel.Info
            );

            // Try GPS first
            var gpsPosition = await GetPreciseLocationAsync();
            
            if (gpsPosition != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ GPS location obtained: {gpsPosition.Latitude}, {gpsPosition.Longitude}",
                    LogLevel.Info
                );

                // For now, we can't reverse geocode without an API key
                // So we'll just return basic info with coordinates
                // In a production app, you'd use a reverse geocoding service here
                return new AddressComponents
                {
                    Country = "Nigeria",
                    CountryCode = "NG",
                    Latitude = gpsPosition.Latitude,
                    Longitude = gpsPosition.Longitude,
                    FormattedAddress = $"Coordinates: {gpsPosition.Latitude:F4}, {gpsPosition.Longitude:F4}"
                };
            }

            await MID_HelperFunctions.DebugMessageAsync(
                "⚠️ GPS failed, falling back to IP-based location",
                LogLevel.Warning
            );

            // Fall back to IP-based location
            return await GetLocationFromIPAsync();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting location");
            _logger.LogError(ex, "Failed to get location");
            
            // Last resort: try IP location
            return await GetLocationFromIPAsync();
        }
    }

    public async Task<AddressComponents?> GetLocationFromIPAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "🌍 Getting location from IP address",
                LogLevel.Info
            );

            var result = await _jsRuntime.InvokeAsync<AddressComponents>(
                "geolocationHelper.getLocationFromIP"
            );

            if (result != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ IP location detected: {result.City}, {result.State}",
                    LogLevel.Info
                );
            }

            return result;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting location from IP");
            _logger.LogError(ex, "Failed to get location from IP");
            return null;
        }
    }

    public async Task<GeolocationPosition?> GetPreciseLocationAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "📍 Requesting precise GPS location",
                LogLevel.Info
            );

            return await _jsRuntime.InvokeAsync<GeolocationPosition>(
                "geolocationHelper.getCurrentPosition"
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting GPS location");
            _logger.LogError(ex, "Failed to get GPS location - user may have denied permission");
            return null;
        }
    }
}

public class GeolocationPosition
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Accuracy { get; set; }
}

public class AddressComponents
{
    public string AddressLine1 { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Country { get; set; } = "Nigeria";
    public string CountryCode { get; set; } = "NG";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string FormattedAddress { get; set; } = "";
}
