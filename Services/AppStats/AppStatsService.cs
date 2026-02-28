// Services/AppStats/AppStatsService.cs
using SubashaVentures.Models.Supabase;
using Supabase;

namespace SubashaVentures.Services.AppStats;

public sealed class AppStatsService : IAppStatsService
{
    private readonly Client _supabase;
    private readonly ILogger<AppStatsService> _logger;

    public AppStatsService(Client supabase, ILogger<AppStatsService> logger)
    {
        _supabase = supabase;
        _logger   = logger;
    }

    public async Task<AppStatSnapshot> GetStatsAsync()
    {
        try
        {
            var response = await _supabase
                .From<AppStatModel>()
                .Get();

            var rows = response.Models ?? new List<AppStatModel>();

            long Get(string key) =>
                rows.FirstOrDefault(r => r.Key == key)?.Value ?? 0;

            return new AppStatSnapshot
            {
                TotalCustomers  = Get("total_customers"),
                TotalProducts   = Get("total_products"),
                OrdersDelivered = Get("orders_delivered"),
                LastUpdated     = rows.Count > 0 ? rows.Max(r => r.UpdatedAt) : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch app_stats");
            return new AppStatSnapshot();
        }
    }
}
