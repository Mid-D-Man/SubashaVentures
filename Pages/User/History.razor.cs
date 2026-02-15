using Microsoft.AspNetCore.Components;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.Orders;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Wishlist;
using SubashaVentures.Domain.Order;
using SubashaVentures.Domain.Product;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.Tracking;
using SubashaVentures.Utilities.ObjectPooling;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.User;

public partial class History : ComponentBase, IDisposable
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IPermissionService PermissionService { get; set; } = default!;
    [Inject] private ProductViewTracker ViewTracker { get; set; } = default!;
    [Inject] private IOrderService OrderService { get; set; } = default!;
    [Inject] private IWishlistService WishlistService { get; set; } = default!;
    [Inject] private IProductService ProductService { get; set; } = default!;
    [Inject] private ILogger<History> Logger { get; set; } = default!;

    private ConfirmationPopup ClearConfirmation { get; set; } = default!;

    private List<ViewedProduct> ViewedProducts = new();
    private List<OrderSummaryDto> RecentOrders = new();
    private List<WishlistHistoryItem> RecentWishlistItems = new();

    private bool IsLoading = true;
    private bool IsAuthenticated = false;
    private string ActiveTab = "viewed";
    private string CurrentUserId = string.Empty;

    private static readonly MID_ComponentObjectPool<List<ViewedProduct>> ViewedProductsPool = 
        new(() => new List<ViewedProduct>(50), list => list.Clear(), maxPoolSize: 10);
    
    private static readonly MID_ComponentObjectPool<List<OrderSummaryDto>> OrdersPool = 
        new(() => new List<OrderSummaryDto>(20), list => list.Clear(), maxPoolSize: 5);
    
    private static readonly MID_ComponentObjectPool<List<WishlistHistoryItem>> WishlistPool = 
        new(() => new List<WishlistHistoryItem>(30), list => list.Clear(), maxPoolSize: 5);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Initializing History page",
                LogLevel.Info
            );

            IsAuthenticated = await PermissionService.IsAuthenticatedAsync();

            if (IsAuthenticated)
            {
                CurrentUserId = await PermissionService.GetCurrentUserIdAsync() ?? string.Empty;
            }

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

            await LoadViewedProducts();

            if (IsAuthenticated && !string.IsNullOrEmpty(CurrentUserId))
            {
                await Task.WhenAll(
                    LoadRecentOrders(),
                    LoadRecentWishlist()
                );
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded history: {ViewedProducts.Count} viewed, {RecentOrders.Count} orders, {RecentWishlistItems.Count} wishlist",
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
                "Loading viewed products from ProductViewTracker",
                LogLevel.Info
            );

            ViewedProducts = await ViewTracker.GetViewedProductsAsync();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded {ViewedProducts.Count} viewed products",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading viewed products");
            Logger.LogError(ex, "Failed to load viewed products");
            ViewedProducts = new List<ViewedProduct>();
        }
    }

    private async Task LoadRecentOrders()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Loading recent orders (last 7 days)",
                LogLevel.Info
            );

            var allOrders = await OrderService.GetUserOrdersAsync(CurrentUserId);
            
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
            RecentOrders = allOrders
                .Where(o => o.CreatedAt >= sevenDaysAgo)
                .OrderByDescending(o => o.CreatedAt)
                .Take(10)
                .ToList();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded {RecentOrders.Count} recent orders",
                LogLevel.Info
            );
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
                "Loading recent wishlist items (last 7 days)",
                LogLevel.Info
            );

            var wishlistModels = await WishlistService.GetUserWishlistAsync(CurrentUserId);
            
            if (!wishlistModels.Any())
            {
                RecentWishlistItems = new List<WishlistHistoryItem>();
                return;
            }

            var wishlist = wishlistModels.First();
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
            
            var recentItems = wishlist.Items
                .Where(i => i.added_at >= sevenDaysAgo)
                .OrderByDescending(i => i.added_at)
                .Take(10)
                .ToList();

            var wishlistHistoryItems = new List<WishlistHistoryItem>();

            foreach (var item in recentItems)
            {
                try
                {
                    var productId = int.Parse(item.product_id);
                    var product = await ProductService.GetProductByIdAsync(productId);

                    if (product != null)
                    {
                        wishlistHistoryItems.Add(new WishlistHistoryItem
                        {
                            ProductId = item.product_id,
                            ProductName = product.Name,
                            ImageUrl = product.Images?.FirstOrDefault() ?? string.Empty,
                            Price = product.Price,
                            IsInStock = product.IsInStock,
                            AddedAt = item.added_at
                        });
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to load product {ProductId} for wishlist history", item.product_id);
                }
            }

            RecentWishlistItems = wishlistHistoryItems;

            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded {RecentWishlistItems.Count} recent wishlist items",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading recent wishlist");
            Logger.LogError(ex, "Failed to load recent wishlist items");
            RecentWishlistItems = new List<WishlistHistoryItem>();
        }
    }

    private async Task RemoveViewedProduct(string productId, Microsoft.AspNetCore.Components.Web.MouseEventArgs e)
    {
        try
        {
            ViewedProducts.RemoveAll(p => p.ProductId == productId);

            var allProducts = await ViewTracker.GetViewedProductsAsync();
            allProducts.RemoveAll(p => p.ProductId == productId);
            
            await ViewTracker.ClearViewedProductsAsync();
            foreach (var product in allProducts)
            {
                await ViewTracker.TrackProductViewAsync(
                    product.ProductId,
                    product.ProductName,
                    product.ImageUrl,
                    product.Price
                );
            }

            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Removed product {productId} from viewed history",
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
        if (ClearConfirmation != null)
        {
            ClearConfirmation.Open();
        }
    }

    private async Task ConfirmClearViewedProducts()
    {
        try
        {
            await ViewTracker.ClearViewedProductsAsync();
            ViewedProducts.Clear();
            
            if (ClearConfirmation != null)
            {
                ClearConfirmation.Close();
            }
            
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                "Cleared all viewed products",
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
        Navigation.NavigateTo($"product/{productId}");
    }

    private void ViewOrder(string orderId)
    {
        Navigation.NavigateTo($"user/orders/{orderId}");
    }

    private void NavigateToShop()
    {
        Navigation.NavigateTo("shop");
    }

    private void NavigateToWishlist()
    {
        Navigation.NavigateTo("user/wishlist-cart");
    }

    public void Dispose()
    {
        ViewedProducts?.Clear();
        RecentOrders?.Clear();
        RecentWishlistItems?.Clear();
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
