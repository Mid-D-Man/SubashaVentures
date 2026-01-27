// Services/Geolocation/GeolocationService.cs
using Microsoft.JSInterop;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Geolocation;

public interface IGeolocationService
{
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

    public async Task<AddressComponents?> GetLocationFromIPAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Getting location from IP address",
                LogLevel.Info
            );

            var result = await _jsRuntime.InvokeAsync<AddressComponents>(
                "geolocationHelper.getLocationFromIP"
            );

            if (result != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"IP location detected: {result.City}, {result.State}",
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
                "Getting precise GPS location",
                LogLevel.Info
            );

            return await _jsRuntime.InvokeAsync<GeolocationPosition>(
                "geolocationHelper.getCurrentPosition"
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting GPS location");
            _logger.LogError(ex, "Failed to get GPS location");
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
