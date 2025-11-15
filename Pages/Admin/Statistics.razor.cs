using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SubashaVentures.Services.Statistics;
using SubashaVentures.Domain.Statistics;
using SubashaVentures.Components.Shared.Notifications;
using SubashaVentures.Utilities.HelperScripts;
using System.Text;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Admin;

public partial class Statistics : ComponentBase
{
    [Inject] private IStatisticsService StatisticsService { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ILogger<Statistics> Logger { get; set; } = default!;

    private NotificationComponent? notificationComponent;

    // State
    private bool isLoading = true;
    private string activeTab = "overview";
    private string selectedDateRange = "30";

    // Data
    private GeneralStatistics? generalStats;
    private List<TopPerformingProduct> topProducts = new();
    private List<CategoryStatistic> categoryStats = new();
    private List<MonthlySalesTrend> monthlySalesTrends = new();
    private List<StockAlert> stockAlerts = new();
    private List<RevenueByCategory> revenueByCategory = new();
    private List<ProductPerformanceMetric> performanceMetrics = new();

    // Pagination
    private int productsCurrentPage = 1;
    private int productsPageSize = 10;
    private int alertsCurrentPage = 1;
    private int alertsPageSize = 10;

    protected override async Task OnInitializedAsync()
    {
        await LoadAllData();
    }

    private async Task LoadAllData()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                "Loading statistics data...",
                LogLevel.Info
            );

            var tasks = new[]
            {
                LoadGeneralStatsAsync(),
                LoadTopProductsAsync(),
                LoadCategoryStatsAsync(),
                LoadMonthlySalesTrendsAsync(),
                LoadStockAlertsAsync(),
                LoadRevenueByCategoryAsync(),
                LoadPerformanceMetricsAsync()
            };

            await Task.WhenAll(tasks);

            await MID_HelperFunctions.DebugMessageAsync(
                "✓ All statistics loaded successfully",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading statistics");
            ShowErrorNotification("Failed to load statistics data");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadGeneralStatsAsync()
    {
        generalStats = await StatisticsService.GetGeneralStatisticsAsync();
    }

    private async Task LoadTopProductsAsync()
    {
        topProducts = await StatisticsService.GetTopProductsAsync(50);
    }

    private async Task LoadCategoryStatsAsync()
    {
        categoryStats = await StatisticsService.GetCategoryStatisticsAsync();
    }

    private async Task LoadMonthlySalesTrendsAsync()
    {
        monthlySalesTrends = await StatisticsService.GetMonthlySalesTrendsAsync(12);
    }

    private async Task LoadStockAlertsAsync()
    {
        stockAlerts = await StatisticsService.GetStockAlertsAsync();
    }

    private async Task LoadRevenueByCategoryAsync()
    {
        revenueByCategory = await StatisticsService.GetRevenueByCategoryAsync();
    }

    private async Task LoadPerformanceMetricsAsync()
    {
        performanceMetrics = await StatisticsService.GetProductPerformanceMetricsAsync(50);
    }

    private void SetActiveTab(string tab)
    {
        activeTab = tab;
        StateHasChanged();
    }

    private async Task HandleDateRangeChange(ChangeEventArgs e)
    {
        selectedDateRange = e.Value?.ToString() ?? "30";
        await RefreshData();
    }

    private async Task RefreshData()
    {
        ShowInfoNotification("Refreshing data...");
        await LoadAllData();
        ShowSuccessNotification("Data refreshed successfully!");
    }

    private async Task ExportData()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            var csv = new StringBuilder();
            
            // General Statistics
            csv.AppendLine("=== GENERAL STATISTICS ===");
            csv.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            csv.AppendLine($"Date Range: Last {selectedDateRange} days");
            csv.AppendLine();
            
            if (generalStats != null)
            {
                csv.AppendLine("Metric,Value");
                csv.AppendLine($"Total Products,{generalStats.TotalProducts}");
                csv.AppendLine($"Active Products,{generalStats.ActiveProducts}");
                csv.AppendLine($"Out of Stock,{generalStats.OutOfStock}");
                csv.AppendLine($"Low Stock,{generalStats.LowStock}");
                csv.AppendLine($"Featured Products,{generalStats.FeaturedProducts}");
                csv.AppendLine($"Products On Sale,{generalStats.ProductsOnSale}");
                csv.AppendLine($"Total Stock Units,{generalStats.TotalStockUnits}");
                csv.AppendLine($"Total Views,{generalStats.TotalViews}");
                csv.AppendLine($"Total Sales,{generalStats.TotalSales}");
                csv.AppendLine($"Total Revenue,{generalStats.TotalRevenue}");
                csv.AppendLine($"Average Price,{generalStats.AveragePrice}");
                csv.AppendLine($"Highest Price,{generalStats.HighestPrice}");
                csv.AppendLine($"Lowest Price,{generalStats.LowestPrice}");
                csv.AppendLine();
            }

            // Top Products
            csv.AppendLine("=== TOP PERFORMING PRODUCTS ===");
            csv.AppendLine("Rank,Name,SKU,Category,Price,Sales,Revenue,Views,Rating,Reviews,Stock");
            for (int i = 0; i < Math.Min(10, topProducts.Count); i++)
            {
                var product = topProducts[i];
                csv.AppendLine($"{i + 1}," +
                              $"\"{EscapeCsv(product.Name)}\"," +
                              $"{product.Sku}," +
                              $"{product.Category}," +
                              $"{product.Price}," +
                              $"{product.SalesCount}," +
                              $"{product.TotalRevenue}," +
                              $"{product.ViewCount}," +
                              $"{product.Rating}," +
                              $"{product.ReviewCount}," +
                              $"{product.Stock}");
            }
            csv.AppendLine();

            // Category Statistics
            csv.AppendLine("=== CATEGORY STATISTICS ===");
            csv.AppendLine("Category,Products,Sales,Revenue,Avg Price,Views,Stock");
            foreach (var category in categoryStats)
            {
                csv.AppendLine($"{category.Category}," +
                              $"{category.ProductCount}," +
                              $"{category.TotalSales}," +
                              $"{category.TotalRevenue}," +
                              $"{category.AvgPrice}," +
                              $"{category.TotalViews}," +
                              $"{category.TotalStock}");
            }
            csv.AppendLine();

            // Monthly Sales Trends
            csv.AppendLine("=== MONTHLY SALES TRENDS ===");
            csv.AppendLine("Month,Products Added,Total Sales,Total Revenue");
            foreach (var trend in monthlySalesTrends)
            {
                csv.AppendLine($"{trend.MonthName}," +
                              $"{trend.ProductsAdded}," +
                              $"{trend.TotalSales}," +
                              $"{trend.TotalRevenue}");
            }

            var fileName = $"subashaventures_analytics_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            var csvBytes = Encoding.UTF8.GetBytes(csv.ToString());
            var base64 = Convert.ToBase64String(csvBytes);

            await JSRuntime.InvokeVoidAsync("downloadFile", fileName, base64, "text/csv");

            ShowSuccessNotification($"Report exported successfully: {fileName}");
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Exported analytics report: {fileName}",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Exporting analytics");
            ShowErrorNotification($"Export failed: {ex.Message}");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private void HandleProductsPageChange(int page)
    {
        productsCurrentPage = page;
        StateHasChanged();
    }

    private void HandleAlertsPageChange(int page)
    {
        alertsCurrentPage = page;
        StateHasChanged();
    }

    private string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        
        if (value.Contains("\""))
            value = value.Replace("\"", "\"\"");
        
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            return value;
        
        return value;
    }

    private void ShowSuccessNotification(string message)
    {
        notificationComponent?.ShowSuccess(message);
    }

    private void ShowErrorNotification(string message)
    {
        notificationComponent?.ShowError(message);
    }

    private void ShowInfoNotification(string message)
    {
        notificationComponent?.ShowInfo(message);
    }
}
