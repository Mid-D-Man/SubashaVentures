// Services/Geolocation/GeolocationService.cs - FIXED TO USE IP FOR ADDRESS
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
    /// Get location - tries GPS first for coordinates, but uses IP for address details
    /// </summary>
    public async Task<AddressComponents?> GetLocationAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "🎯 Attempting to get location (GPS for coordinates, IP for address)",
                LogLevel.Info
            );

            // Try GPS first for precise coordinates
            var gpsPosition = await GetPreciseLocationAsync();
            
            if (gpsPosition != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ GPS location obtained: {gpsPosition.Latitude}, {gpsPosition.Longitude}",
                    LogLevel.Info
                );

                // We have GPS coordinates, but we need city/state names
                // Since we don't have reverse geocoding API, use IP geolocation for address details
                var ipLocation = await GetLocationFromIPAsync();
                
                if (ipLocation != null)
                {
                    // Combine GPS coordinates with IP address details
                    ipLocation.Latitude = gpsPosition.Latitude;
                    ipLocation.Longitude = gpsPosition.Longitude;
                    
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✅ Combined GPS coordinates with IP address: {ipLocation.City}, {ipLocation.State}",
                        LogLevel.Info
                    );
                    
                    return ipLocation;
                }
            }

            await MID_HelperFunctions.DebugMessageAsync(
                "⚠️ GPS failed or permission denied, using IP-based location",
                LogLevel.Warning
            );

            // Fall back to IP-based location only
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
