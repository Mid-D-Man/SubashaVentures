using Microsoft.AspNetCore.Components;
using SubashaVentures.Domain.Order;
using SubashaVentures.Services.Orders;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;
using System.Linq; 

namespace SubashaVentures.Pages.User;

public partial class Orders : ComponentBase
{
    [Inject] private IOrderService OrderService { get; set; } = default!;
    [Inject] private IPermissionService PermissionService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<Orders> Logger { get; set; } = default!;

    // State
    private List<OrderViewModel> OrdersList = new();
    private List<OrderViewModel> FilteredOrders = new();
    private bool IsLoading = true;
    private bool IsLoadingMore = false;
    private bool HasMoreOrders = false;
    private string FilterStatus = "All";
    private string? CurrentUserId;
    
    // Pagination
    private int PageSize = 10;
    private int CurrentPage = 0;
    
    // Computed
    private decimal TotalSpent => OrdersList.Sum(o => o.Total);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Ensure authenticated
            if (!await PermissionService.EnsureAuthenticatedAsync("user/orders"))
            {
                Navigation.NavigateTo("signin", true);
                return;
            }

            CurrentUserId = await PermissionService.GetCurrentUserIdAsync();

            if (string.IsNullOrEmpty(CurrentUserId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ User ID not found",
                    LogLevel.Error
                );
                Navigation.NavigateTo("signin", true);
                return;
            }

            await LoadOrders();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Initializing orders page");
            Logger.LogError(ex, "Failed to initialize orders page");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadOrders()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"📦 Loading orders for user: {CurrentUserId}",
                LogLevel.Info
            );

            var orders = await OrderService.GetUserOrdersAsync(
                CurrentUserId!,
                skip: CurrentPage * PageSize,
                take: PageSize + 1
            );

            HasMoreOrders = orders.Count > PageSize;
            var pageOrders = orders.Take(PageSize).ToList();

            if (CurrentPage == 0)
            {
                OrdersList = pageOrders;
            }
            else
            {
                OrdersList.AddRange(pageOrders);
            }

            FilteredOrders = OrdersList;

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Loaded {pageOrders.Count} orders (Page {CurrentPage + 1})",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading orders");
            Logger.LogError(ex, "Failed to load orders");
            OrdersList = new List<OrderViewModel>();
            FilteredOrders = new List<OrderViewModel>();
        }
    }

    private async Task LoadMoreOrders()
    {
        if (IsLoadingMore || !HasMoreOrders) return;

        IsLoadingMore = true;
        StateHasChanged();

        try
        {
            CurrentPage++;
            await LoadOrders();
        }
        finally
        {
            IsLoadingMore = false;
            StateHasChanged();
        }
    }

    private void FilterOrders(string status)
    {
        FilterStatus = status;
        
        FilteredOrders = status == "All"
            ? OrdersList
            : OrdersList.Where(o => o.Status.ToString() == status).ToList();

        StateHasChanged();

        MID_HelperFunctions.DebugMessageAsync(
            $"🔍 Filtered orders by {status}: {FilteredOrders.Count} results",
            LogLevel.Info
        );
    }

    private int GetStatusCount(OrderStatus status)
    {
        return OrdersList.Count(o => o.Status == status);
    }

    private string GetStatusClass(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "status-pending",
        OrderStatus.Processing => "status-processing",
        OrderStatus.Shipped => "status-shipped",
        OrderStatus.Delivered => "status-delivered",
        OrderStatus.Cancelled => "status-cancelled",
        OrderStatus.Refunded => "status-refunded",
        OrderStatus.Failed => "status-failed",
        _ => ""
    };

    private string GetStatusIcon(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "⏳",
        OrderStatus.Processing => "⚙️",
        OrderStatus.Shipped => "🚚",
        OrderStatus.Delivered => "✅",
        OrderStatus.Cancelled => "❌",
        OrderStatus.Refunded => "💰",
        OrderStatus.Failed => "⚠️",
        _ => "📦"
    };

    private string GetPaymentStatusClass(PaymentStatus status) => status switch
    {
        PaymentStatus.Paid => "payment-paid",
        PaymentStatus.Pending => "payment-pending",
        PaymentStatus.Failed => "payment-failed",
        PaymentStatus.Refunded => "payment-refunded",
        PaymentStatus.PartiallyRefunded => "payment-partially-refunded",
        _ => ""
    };

    private string GetPaymentIcon(PaymentMethod method) => method switch
    {
        PaymentMethod.Card => "💳",
        PaymentMethod.BankTransfer => "🏦",
        PaymentMethod.USSD => "📱",
        PaymentMethod.PayOnDelivery => "💵",
        PaymentMethod.Wallet => "👛",
        _ => "💰"
    };

    private void TrackOrder(string orderId)
    {
        Navigation.NavigateTo($"user/orders/track/{orderId}");
    }

    private void ViewOrder(string orderId)
    {
        Navigation.NavigateTo($"user/orders/{orderId}");
    }

    private async Task CancelOrder(string orderId)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"🚫 Cancelling order: {orderId}",
                LogLevel.Info
            );

            var success = await OrderService.CancelOrderAsync(orderId, CurrentUserId!);

            if (success)
            {
                CurrentPage = 0;
                await LoadOrders();

                await MID_HelperFunctions.DebugMessageAsync(
                    "✅ Order cancelled successfully",
                    LogLevel.Info
                );
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ Failed to cancel order",
                    LogLevel.Error
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Cancelling order");
            Logger.LogError(ex, "Failed to cancel order: {OrderId}", orderId);
        }
    }

    private void NavigateToShop()
    {
        Navigation.NavigateTo("shop");
    }
}
