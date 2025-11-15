// Domain/Statistics/StatisticsViewModel.cs
namespace SubashaVentures.Domain.Statistics;

public class GeneralStatistics
{
    public int TotalProducts { get; set; }
    public int ActiveProducts { get; set; }
    public int OutOfStock { get; set; }
    public int LowStock { get; set; }
    public int FeaturedProducts { get; set; }
    public int ProductsOnSale { get; set; }
    public long TotalStockUnits { get; set; }
    public long TotalViews { get; set; }
    public long TotalSales { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal HighestPrice { get; set; }
    public decimal LowestPrice { get; set; }
    
    public string FormattedRevenue => $"₦{TotalRevenue:N0}";
    public string FormattedAvgPrice => $"₦{AveragePrice:N0}";
}

public class TopPerformingProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    public int SalesCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public int ViewCount { get; set; }
    public float Rating { get; set; }
    public int ReviewCount { get; set; }
    public int Stock { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public string FormattedRevenue => $"₦{TotalRevenue:N0}";
    public string FormattedPrice => $"₦{Price:N0}";
}

public class CategoryStatistic
{
    public string Category { get; set; } = "";
    public int ProductCount { get; set; }
    public long TotalSales { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AvgPrice { get; set; }
    public long TotalViews { get; set; }
    public long TotalStock { get; set; }
    
    public string FormattedRevenue => $"₦{TotalRevenue:N0}";
    public string FormattedAvgPrice => $"₦{AvgPrice:N0}";
}

public class MonthlySalesTrend
{
    public DateTime Month { get; set; }
    public int ProductsAdded { get; set; }
    public long TotalSales { get; set; }
    public decimal TotalRevenue { get; set; }
    
    public string MonthName => Month.ToString("MMM yyyy");
    public string FormattedRevenue => $"₦{TotalRevenue:N0}";
}

public class StockAlert
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";
    public string Category { get; set; } = "";
    public int Stock { get; set; }
    public string AlertLevel { get; set; } = "";
    
    public string AlertColorClass => AlertLevel switch
    {
        "Out of Stock" => "alert-critical",
        "Critical" => "alert-danger",
        "Low" => "alert-warning",
        _ => "alert-normal"
    };
}

public class RevenueByCategory
{
    public string Category { get; set; } = "";
    public decimal Revenue { get; set; }
    public int ProductCount { get; set; }
    public decimal Percentage { get; set; }
    
    public string FormattedRevenue => $"₦{Revenue:N0}";
    public string PercentageDisplay => $"{Percentage:F1}%";
}

public class ProductPerformanceMetric
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";
    public int ViewCount { get; set; }
    public int AddToCartCount { get; set; }
    public int PurchaseCount { get; set; }
    public decimal ViewToCartRate { get; set; }
    public decimal CartToPurchaseRate { get; set; }
    public decimal OverallConversionRate { get; set; }
    
    public string ViewToCartDisplay => $"{ViewToCartRate:F1}%";
    public string CartToPurchaseDisplay => $"{CartToPurchaseRate:F1}%";
    public string ConversionDisplay => $"{OverallConversionRate:F1}%";
}
