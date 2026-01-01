// Pages/User/History.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.Products;
using SubashaVentures.Domain.Order;
using SubashaVentures.Utilities.HelperScripts;
using System.Text.Json;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.User;

public partial class History : ComponentBase
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IPermissionService PermissionService { get; set; } = default!;
    [Inject] private IProductService ProductService { get; set; } = default!;
    [Inject] private ILogger<History> Logger { get; set; } = default!;

    private ConfirmationPopup ClearConfirmation { get; set; } = default!;

    private List<ViewedProductItem> ViewedProducts = new();
    private List<OrderSummaryDto> RecentOrders = new();
    private List<WishlistHistoryItem> RecentWishlistItems = new();

    private bool IsLoading = true;
    private bool IsAuthenticated = false;
    private string ActiveTab = "viewed";

    private const string VIEWED_PRODUCTS_KEY = "viewed_products_history";
    private const int MAX_VIEWED_PRODUCTS = 50;
    private const int RECENT_DAYS_THRESHOLD = 30;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "🔄 Initializing History page",
                LogLevel.Info
            );

            IsAuthenticated = await PermissionService.IsAuthenticatedAsync();

            await LoadHistory();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Initializing History page");
            Logger.LogError(ex, "Failed to initialize History page");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadHistory()
    {
        try
        {
            IsLoading = true;
            StateHasChanged();

            // Always load viewed products (local storage)
            await LoadViewedProducts();

            // Load authenticated user data
            if (IsAuthenticated)
            {
                await LoadRecentOrders();
                await LoadRecentWishlist();
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Loaded history: {ViewedProducts.Count} viewed, {RecentOrders.Count} orders, {RecentWishlistItems.Count} wishlist",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading history");
            Logger.LogError(ex, "Failed to load history");
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadViewedProducts()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "📥 Loading viewed products from localStorage",
                LogLevel.Info
            );

            var historyJson = await JS.InvokeAsync<string>("localStorage.getItem", VIEWED_PRODUCTS_KEY);

            if (!string.IsNullOrEmpty(historyJson))
            {
                var items = JsonSerializer.Deserialize<List<ViewedProductItem>>(historyJson);
                
                if (items != null)
                {
                    // Filter to last 30 days and sort by most recent
                    var cutoffDate = DateTime.UtcNow.AddDays(-RECENT_DAYS_THRESHOLD);
                    ViewedProducts = items
                        .Where(i => i.ViewedAt >= cutoffDate)
                        .OrderByDescending(i => i.ViewedAt)
                        .Take(MAX_VIEWED_PRODUCTS)
                        .ToList();

                    // If we filtered any items, save the cleaned list back
                    if (ViewedProducts.Count != items.Count)
                    {
                        await SaveViewedProducts();
                    }

                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✅ Loaded {ViewedProducts.Count} viewed products",
                        LogLevel.Info
                    );
                }
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading viewed products");
            Logger.LogError(ex, "Failed to load viewed products");
            ViewedProducts = new List<ViewedProductItem>();
        }
    }

    private async Task LoadRecentOrders()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "📦 Loading recent orders",
                LogLevel.Info
            );

            // TODO: Implement when OrderService is available
            // For now, return empty list
            RecentOrders = new List<OrderSummaryDto>();

            /* When OrderService is ready:
            var userId = await PermissionService.GetCurrentUserIdAsync();
            if (!string.IsNullOrEmpty(userId))
            {
                var allOrders = await OrderService.GetUserOrdersAsync(userId);
                var cutoffDate = DateTime.UtcNow.AddDays(-RECENT_DAYS_THRESHOLD);
                
                RecentOrders = allOrders
                    .Where(o => o.CreatedAt >= cutoffDate)
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(10)
                    .ToList();
            }
            */
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading recent orders");
            Logger.LogError(ex, "Failed to load recent orders");
            RecentOrders = new List<OrderSummaryDto>();
        }
    }

    private async Task LoadRecentWishlist()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "💝 Loading recent wishlist items",
                LogLevel.Info
            );

            // TODO: Implement when we have wishlist with timestamps
            // For now, return empty list
            RecentWishlistItems = new List<WishlistHistoryItem>();

            /* When WishlistService supports UpdatedAt:
            var userId = await PermissionService.GetCurrentUserIdAsync();
            if (!string.IsNullOrEmpty(userId))
            {
                var wishlistModels = await WishlistService.GetUserWishlistAsync(userId);
                if (wishlistModels.Any())
                {
                    var cutoffDate = DateTime.UtcNow.AddDays(-RECENT_DAYS_THRESHOLD);
                    var wishlist = wishlistModels.First();
                    
                    var recentItems = wishlist.Items
                        .Where(i => i.added_at >= cutoffDate)
                        .OrderByDescending(i => i.added_at)
                        .Take(10)
                        .ToList();

                    foreach (var item in recentItems)
                    {
                        if (int.TryParse(item.product_id, out var productId))
                        {
                            var product = await ProductService.GetProductByIdAsync(productId);
                            if (product != null)
                            {
                                RecentWishlistItems.Add(new WishlistHistoryItem
                                {
                                    ProductId = item.product_id,
                                    ProductName = product.Name,
                                    ImageUrl = product.Images?.FirstOrDefault() ?? "/images/placeholder.jpg",
                                    Price = product.Price,
                                    IsInStock = product.IsInStock,
                                    AddedAt = item.added_at
                                });
                            }
                        }
                    }
                }
            }
            */
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading recent wishlist");
            Logger.LogError(ex, "Failed to load recent wishlist items");
            RecentWishlistItems = new List<WishlistHistoryItem>();
        }
    }

    private async Task SaveViewedProducts()
    {
        try
        {
            var json = JsonSerializer.Serialize(ViewedProducts);
            await JS.InvokeVoidAsync("localStorage.setItem", VIEWED_PRODUCTS_KEY, json);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save viewed products");
        }
    }

    private async Task RemoveViewedProduct(string productId, MouseEventArgs e)
    {
        try
        {
            ViewedProducts.RemoveAll(p => p.ProductId == productId);
            await SaveViewedProducts();
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Removed product {productId} from viewed history",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to remove viewed product");
        }
    }

    private void ClearViewedProducts()
    {
        ClearConfirmation.Open();
    }

    private async Task ConfirmClearViewedProducts()
    {
        try
        {
            ViewedProducts.Clear();
            await SaveViewedProducts();
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                "🗑️ Cleared all viewed products",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to clear viewed products");
        }
    }

    private void ViewProduct(string productId)
    {
        Navigation.NavigateTo($"/products/{productId}");
    }

    private void ViewOrder(string orderId)
    {
        Navigation.NavigateTo($"/user/orders/{orderId}");
    }

    private void NavigateToShop()
    {
        Navigation.NavigateTo("/shop");
    }

    private void NavigateToWishlist()
    {
        Navigation.NavigateTo("/user/wishlist");
    }

    // Helper classes
    public class ViewedProductItem
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public DateTime ViewedAt { get; set; }

        public string ViewedTime
        {
            get
            {
                var span = DateTime.UtcNow - ViewedAt;
                if (span.TotalMinutes < 60)
                    return $"{(int)span.TotalMinutes}m ago";
                if (span.TotalHours < 24)
                    return $"{(int)span.TotalHours}h ago";
                if (span.TotalDays < 7)
                    return $"{(int)span.TotalDays}d ago";
                return ViewedAt.ToString("MMM dd");
            }
        }
    }

    public class WishlistHistoryItem
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsInStock { get; set; }
        public DateTime AddedAt { get; set; }

        public string AddedTime
        {
            get
            {
                var span = DateTime.UtcNow - AddedAt;
                if (span.TotalMinutes < 60)
                    return $"{(int)span.TotalMinutes}m ago";
                if (span.TotalHours < 24)
                    return $"{(int)span.TotalHours}h ago";
                if (span.TotalDays < 7)
                    return $"{(int)span.TotalDays}d ago";
                return AddedAt.ToString("MMM dd");
            }
        }
    }
}
