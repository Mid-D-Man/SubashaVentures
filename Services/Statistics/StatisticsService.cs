// Services/Statistics/StatisticsService.cs
using SubashaVentures.Domain.Statistics;
using SubashaVentures.Services.SupaBase;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Postgrest;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Statistics;

public class StatisticsService : IStatisticsService
{
    private readonly ISupabaseDatabaseService _database;
    private readonly ILogger<StatisticsService> _logger;
    private readonly Client _supabaseClient;

    public StatisticsService(
        ISupabaseDatabaseService database,
        Supabase.Client supabaseClient,
        ILogger<StatisticsService> logger)
    {
        _database = database;
        _supabaseClient = supabaseClient;
        _logger = logger;
    }

    public async Task<GeneralStatistics?> GetGeneralStatisticsAsync()
    {
        try
        {
            var result = await _supabaseClient
                .From<GeneralStatisticsModel>()
                .Get();

            if (result?.Models == null || !result.Models.Any())
                return null;

            var model = result.Models.First();
            return MapToGeneralStatistics(model);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting general statistics");
            return null;
        }
    }

    public async Task<List<TopPerformingProduct>> GetTopProductsAsync(int limit = 10)
    {
        try
        {
            var result = await _supabaseClient
                .From<TopPerformingProductModel>()
                .Limit(limit)
                .Get();

            return result?.Models?.Select(MapToTopProduct).ToList() 
                   ?? new List<TopPerformingProduct>();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting top products");
            return new List<TopPerformingProduct>();
        }
    }

    public async Task<List<CategoryStatistic>> GetCategoryStatisticsAsync()
    {
        try
        {
            var result = await _supabaseClient
                .From<CategoryStatisticModel>()
                .Get();

            return result?.Models?.Select(MapToCategoryStatistic).ToList() 
                   ?? new List<CategoryStatistic>();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting category statistics");
            return new List<CategoryStatistic>();
        }
    }

    public async Task<List<MonthlySalesTrend>> GetMonthlySalesTrendsAsync(int months = 12)
    {
        try
        {
            var result = await _supabaseClient
                .From<MonthlySalesTrendModel>()
                .Limit(months)
                .Get();

            return result?.Models?.Select(MapToMonthlySalesTrend).ToList() 
                   ?? new List<MonthlySalesTrend>();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting monthly sales trends");
            return new List<MonthlySalesTrend>();
        }
    }

    public async Task<List<StockAlert>> GetStockAlertsAsync()
    {
        try
        {
            var result = await _supabaseClient
                .From<StockAlertModel>()
                .Get();

            return result?.Models?.Select(MapToStockAlert).ToList() 
                   ?? new List<StockAlert>();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting stock alerts");
            return new List<StockAlert>();
        }
    }

    public async Task<List<RevenueByCategory>> GetRevenueByCategoryAsync()
    {
        try
        {
            var result = await _supabaseClient
                .From<RevenueByCategoryModel>()
                .Get();

            return result?.Models?.Select(MapToRevenueByCategory).ToList() 
                   ?? new List<RevenueByCategory>();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting revenue by category");
            return new List<RevenueByCategory>();
        }
    }

    public async Task<List<ProductPerformanceMetric>> GetProductPerformanceMetricsAsync(int limit = 20)
    {
        try
        {
            var result = await _supabaseClient
                .From<ProductPerformanceMetricModel>()
                .Limit(limit)
                .Get();

            return result?.Models?.Select(MapToPerformanceMetric).ToList() 
                   ?? new List<ProductPerformanceMetric>();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting performance metrics");
            return new List<ProductPerformanceMetric>();
        }
    }

    // Mapping methods
    private GeneralStatistics MapToGeneralStatistics(GeneralStatisticsModel model) => new()
    {
        TotalProducts = model.TotalProducts,
        ActiveProducts = model.ActiveProducts,
        OutOfStock = model.OutOfStock,
        LowStock = model.LowStock,
        FeaturedProducts = model.FeaturedProducts,
        ProductsOnSale = model.ProductsOnSale,
        TotalStockUnits = model.TotalStockUnits,
        TotalViews = model.TotalViews,
        TotalSales = model.TotalSales,
        TotalRevenue = model.TotalRevenue,
        AveragePrice = model.AveragePrice,
        HighestPrice = model.HighestPrice,
        LowestPrice = model.LowestPrice
    };

    private TopPerformingProduct MapToTopProduct(TopPerformingProductModel model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Sku = model.Sku,
        Category = model.Category,
        Price = model.Price,
        SalesCount = model.SalesCount,
        TotalRevenue = model.TotalRevenue,
        ViewCount = model.ViewCount,
        Rating = model.Rating,
        ReviewCount = model.ReviewCount,
        Stock = model.Stock,
        CreatedAt = model.CreatedAt
    };

    private CategoryStatistic MapToCategoryStatistic(CategoryStatisticModel model) => new()
    {
        Category = model.Category,
        ProductCount = model.ProductCount,
        TotalSales = model.TotalSales,
        TotalRevenue = model.TotalRevenue,
        AvgPrice = model.AvgPrice,
        TotalViews = model.TotalViews,
        TotalStock = model.TotalStock
    };

    private MonthlySalesTrend MapToMonthlySalesTrend(MonthlySalesTrendModel model) => new()
    {
        Month = model.Month,
        ProductsAdded = model.ProductsAdded,
        TotalSales = model.TotalSales,
        TotalRevenue = model.TotalRevenue
    };

    private StockAlert MapToStockAlert(StockAlertModel model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Sku = model.Sku,
        Category = model.Category,
        Stock = model.Stock,
        AlertLevel = model.AlertLevel
    };

    private RevenueByCategory MapToRevenueByCategory(RevenueByCategoryModel model) => new()
    {
        Category = model.Category,
        Revenue = model.Revenue,
        ProductCount = model.ProductCount,
        Percentage = model.Percentage
    };

    private ProductPerformanceMetric MapToPerformanceMetric(ProductPerformanceMetricModel model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Sku = model.Sku,
        ViewCount = model.ViewCount,
        AddToCartCount = model.AddToCartCount,
        PurchaseCount = model.PurchaseCount,
        ViewToCartRate = model.ViewToCartRate,
        CartToPurchaseRate = model.CartToPurchaseRate,
        OverallConversionRate = model.OverallConversionRate
    };

    // Supabase Models
    [Table("general_statistics")]
    private class GeneralStatisticsModel : BaseModel
    {
        [Column("total_products")] public int TotalProducts { get; set; }
        [Column("active_products")] public int ActiveProducts { get; set; }
        [Column("out_of_stock")] public int OutOfStock { get; set; }
        [Column("low_stock")] public int LowStock { get; set; }
        [Column("featured_products")] public int FeaturedProducts { get; set; }
        [Column("products_on_sale")] public int ProductsOnSale { get; set; }
        [Column("total_stock_units")] public long TotalStockUnits { get; set; }
        [Column("total_views")] public long TotalViews { get; set; }
        [Column("total_sales")] public long TotalSales { get; set; }
        [Column("total_revenue")] public decimal TotalRevenue { get; set; }
        [Column("average_price")] public decimal AveragePrice { get; set; }
        [Column("highest_price")] public decimal HighestPrice { get; set; }
        [Column("lowest_price")] public decimal LowestPrice { get; set; }
    }

    [Table("top_products_last_30_days")]
    private class TopPerformingProductModel : BaseModel
    {
        [Column("id")] public int Id { get; set; }
        [Column("name")] public string Name { get; set; } = "";
        [Column("sku")] public string Sku { get; set; } = "";
        [Column("category")] public string Category { get; set; } = "";
        [Column("price")] public decimal Price { get; set; }
        [Column("sales_count")] public int SalesCount { get; set; }
        [Column("total_revenue")] public decimal TotalRevenue { get; set; }
        [Column("view_count")] public int ViewCount { get; set; }
        [Column("rating")] public float Rating { get; set; }
        [Column("review_count")] public int ReviewCount { get; set; }
        [Column("stock")] public int Stock { get; set; }
        [Column("created_at")] public DateTime CreatedAt { get; set; }
    }

    [Table("category_statistics")]
    private class CategoryStatisticModel : BaseModel
    {
        [Column("category")] public string Category { get; set; } = "";
        [Column("product_count")] public int ProductCount { get; set; }
        [Column("total_sales")] public long TotalSales { get; set; }
        [Column("total_revenue")] public decimal TotalRevenue { get; set; }
        [Column("avg_price")] public decimal AvgPrice { get; set; }
        [Column("total_views")] public long TotalViews { get; set; }
        [Column("total_stock")] public long TotalStock { get; set; }
    }

    [Table("monthly_sales_trends")]
    private class MonthlySalesTrendModel : BaseModel
    {
        [Column("month")] public DateTime Month { get; set; }
        [Column("products_added")] public int ProductsAdded { get; set; }
        [Column("total_sales")] public long TotalSales { get; set; }
        [Column("total_revenue")] public decimal TotalRevenue { get; set; }
    }

    [Table("stock_alerts")]
    private class StockAlertModel : BaseModel
    {
        [Column("id")] public int Id { get; set; }
        [Column("name")] public string Name { get; set; } = "";
        [Column("sku")] public string Sku { get; set; } = "";
        [Column("category")] public string Category { get; set; } = "";
        [Column("stock")] public int Stock { get; set; }
        [Column("alert_level")] public string AlertLevel { get; set; } = "";
    }

    [Table("revenue_by_category")]
    private class RevenueByCategoryModel : BaseModel
    {
        [Column("category")] public string Category { get; set; } = "";
        [Column("revenue")] public decimal Revenue { get; set; }
        [Column("product_count")] public int ProductCount { get; set; }
        [Column("percentage")] public decimal Percentage { get; set; }
    }

    [Table("product_performance_metrics")]
    private class ProductPerformanceMetricModel : BaseModel
    {
        [Column("id")] public int Id { get; set; }
        [Column("name")] public string Name { get; set; } = "";
        [Column("sku")] public string Sku { get; set; } = "";
        [Column("view_count")] public int ViewCount { get; set; }
        [Column("add_to_cart_count")] public int AddToCartCount { get; set; }
        [Column("purchase_count")] public int PurchaseCount { get; set; }
        [Column("view_to_cart_rate")] public decimal ViewToCartRate { get; set; }
        [Column("cart_to_purchase_rate")] public decimal CartToPurchaseRate { get; set; }
        [Column("overall_conversion_rate")] public decimal OverallConversionRate { get; set; }
    }
}
