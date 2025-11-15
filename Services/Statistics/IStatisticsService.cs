// Services/Statistics/IStatisticsService.cs
using SubashaVentures.Domain.Statistics;

namespace SubashaVentures.Services.Statistics;

public interface IStatisticsService
{
    Task<GeneralStatistics?> GetGeneralStatisticsAsync();
    Task<List<TopPerformingProduct>> GetTopProductsAsync(int limit = 10);
    Task<List<CategoryStatistic>> GetCategoryStatisticsAsync();
    Task<List<MonthlySalesTrend>> GetMonthlySalesTrendsAsync(int months = 12);
    Task<List<StockAlert>> GetStockAlertsAsync();
    Task<List<RevenueByCategory>> GetRevenueByCategoryAsync();
    Task<List<ProductPerformanceMetric>> GetProductPerformanceMetricsAsync(int limit = 20);
}
