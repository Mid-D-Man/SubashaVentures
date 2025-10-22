
namespace SubashaVentures.Services.Time
{
    public interface IServerTimeService
    {
        Task<DateTime> GetCurrentServerTimeAsync();
        Task<bool> SyncWithServerAsync();
        bool IsBusinessHours();
        DateTime GetCachedServerTime();
    }
}