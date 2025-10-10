using Microsoft.AspNetCore.Components;

namespace SubashaVentures.Pages.Admin;

public partial class AdminDashboard : ComponentBase
{
    // Dashboard stats
    private decimal totalRevenue = 45231.89m;
    private int totalOrders = 1234;
    private int totalCustomers = 892;
    private int totalProducts = 567;
    
    // Change percentages
    private decimal revenueChange = 20.1m;
    private decimal ordersChange = 15.0m;
    private decimal customersChange = 8.0m;
    
    // Alerts
    private int lowStockCount = 5;
    private int pendingOrdersCount = 12;
    
    protected override async Task OnInitializedAsync()
    {
        // Load dashboard data
        await LoadDashboardData();
    }

    private async Task LoadDashboardData()
    {
        // Simulate API call
        await Task.Delay(100);
        
        // In real app, fetch data from service:
        // totalRevenue = await DashboardService.GetTotalRevenue();
        // totalOrders = await DashboardService.GetTotalOrders();
        // etc.
    }

    private void NavigateToOrders()
    {
        // Navigate to orders page
        // NavigationManager.NavigateTo("admin/orders");
    }

    private void NavigateToProducts()
    {
        // Navigate to products page
        // NavigationManager.NavigateTo("admin/products");
    }

    private void ViewLowStockItems()
    {
        // Navigate to low stock items
        // NavigationManager.NavigateTo("admin/products?filter=low-stock");
    }

    private void ReviewPendingOrders()
    {
        // Navigate to pending orders
        // NavigationManager.NavigateTo("admin/orders?status=pending");
    }

    // Data models for future use
    public class DashboardStats
    {
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
        public int TotalCustomers { get; set; }
        public int TotalProducts { get; set; }
        public decimal RevenueChangePercent { get; set; }
        public decimal OrdersChangePercent { get; set; }
        public decimal CustomersChangePercent { get; set; }
    }

    public class RecentOrder
    {
        public string OrderId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public DateTime OrderDate { get; set; }
    }

    public class TopProduct
    {
        public string Name { get; set; } = string.Empty;
        public int Sales { get; set; }
        public decimal Revenue { get; set; }
    }
}
