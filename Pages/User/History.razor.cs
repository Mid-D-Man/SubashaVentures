// Pages/User/History.razor.cs - FIXED WITH ProductViewTracker

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Domain.Order;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.Tracking;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.User;

public partial class History : ComponentBase
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IPermissionService PermissionService { get; set; } = default!;
    [Inject] private ProductViewTracker ViewTracker { get; set; } = default!;
    [Inject] private ILogger<History> Logger { get; set; } = default!;

    private ConfirmationPopup ClearConfirmation { get; set; } = default!;

    private List<ViewedProduct> ViewedProducts = new();
    private List<OrderSummaryDto> RecentOrders = new();
    private List<WishlistHistoryItem> RecentWishlistItems = new();

    private bool IsLoading = true;
    private bool IsAuthenticated = false;
    private string ActiveTab = "viewed";

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

            // Load viewed products using ProductViewTracker
            await LoadViewedProducts();

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
                "📥 Loading viewed products from ProductViewTracker",
                LogLevel.Info
            );

            // Use ProductViewTracker to get viewed products
            ViewedProducts = await ViewTracker.GetViewedProductsAsync();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Loaded {ViewedProducts.Count} viewed products",
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
                "📦 Loading recent orders",
                LogLevel.Info
            );

            // TODO: Implement when order service is ready
            RecentOrders = new List<OrderSummaryDto>();
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

            // TODO: Implement when wishlist history is ready
            RecentWishlistItems = new List<WishlistHistoryItem>();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading recent wishlist");
            Logger.LogError(ex, "Failed to load recent wishlist items");
            RecentWishlistItems = new List<WishlistHistoryItem>();
        }
    }

    private async Task RemoveViewedProduct(string productId, MouseEventArgs e)
    {
        try
        {
            // Remove from local list
            ViewedProducts.RemoveAll(p => p.ProductId == productId);

            // Get all products, remove the one we want, then save back
            var allProducts = await ViewTracker.GetViewedProductsAsync();
            allProducts.RemoveAll(p => p.ProductId == productId);
            
            // Save updated list back (need to clear and re-add)
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
            await ViewTracker.ClearViewedProductsAsync();
            ViewedProducts.Clear();
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
        Navigation.NavigateTo($"/product/{productId}");
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
