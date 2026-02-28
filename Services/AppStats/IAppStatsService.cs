// Services/AppStats/IAppStatsService.cs
namespace SubashaVentures.Services.AppStats;

public interface IAppStatsService
{
    Task<AppStatSnapshot> GetStatsAsync();
}

public sealed class AppStatSnapshot
{
    public long TotalCustomers  { get; init; }
    public long TotalProducts   { get; init; }
    public long OrdersDelivered { get; init; }
    public DateTime? LastUpdated { get; init; }
}
