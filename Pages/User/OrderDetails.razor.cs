using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SubashaVentures.Domain.Order;
using SubashaVentures.Services.Orders;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.VisualElements;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Domain.Enums;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.User;

public partial class OrderDetails : ComponentBase
{
    [Parameter] public string OrderId { get; set; } = string.Empty;

    [Inject] private IOrderService OrderService { get; set; } = default!;
    [Inject] private IPermissionService PermissionService { get; set; } = default!;
    [Inject] private IVisualElementsService VisualElements { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ILogger<OrderDetails> Logger { get; set; } = default!;

    // State
    private OrderViewModel? Order;
    private bool IsLoading = true;
    private string? CurrentUserId;
    private HashSet<string> ReviewedProducts = new();
    
    // SVG Icon References
    private ElementReference errorIconRef;
    private ElementReference backIconRef;
    private ElementReference navBackIconRef;
    private ElementReference calendarIconRef;
    private ElementReference trackIconRef;
    private ElementReference addressIconRef;
    private ElementReference shippingIconRef;
    private ElementReference trackingIconRef;
    private ElementReference paymentIconRef;
    private ElementReference statusIconRef;
    private ElementReference referenceIconRef;
    private ElementReference userIconRef;
    private ElementReference emailIconRef;
    private ElementReference phoneIconRef;
    private ElementReference cancelIconRef;
    private ElementReference supportIconRef;

    // Computed
    private bool NeedsSupportButton => Order != null && 
        (Order.Status == OrderStatus.Failed || 
         Order.Status == OrderStatus.Cancelled ||
         Order.Status == OrderStatus.Delivered);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Ensure authenticated
            if (!await PermissionService.EnsureAuthenticatedAsync($"user/orders/details/{OrderId}"))
            {
                Navigation.NavigateTo("signin", true);
                return;
            }

            CurrentUserId = await PermissionService.GetCurrentUserIdAsync();

            if (string.IsNullOrEmpty(CurrentUserId))
            {
                Navigation.NavigateTo("signin", true);
                return;
            }

            await LoadOrderDetails();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Initializing order details");
            Logger.LogError(ex, "Failed to initialize order details");
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await LoadSvgIcons();
        }
    }

    private async Task LoadSvgIcons()
    {
        try
        {
            // Error icon
            var errorSvg = await VisualElements.GetSvgAsync(SvgType.Warning, 80, 80);
            await JSRuntime.InvokeVoidAsync("eval", 
                $"document.querySelector('.error-icon').innerHTML = `{errorSvg}`;");

            // Navigation icons
            var backSvg = await VisualElements.GetSvgAsync(SvgType.Close, 20, 20);
            await JSRuntime.InvokeVoidAsync("eval",
                $"document.querySelectorAll('.btn-back .icon, .btn-back-nav .icon').forEach(el => el.innerHTML = `{backSvg}`);");

            // Calendar icon
            var calendarSvg = await VisualElements.GetSvgAsync(SvgType.History, 16, 16);
            await JSRuntime.InvokeVoidAsync("eval",
                $"document.querySelector('.order-date .icon').innerHTML = `{calendarSvg}`;");

            // Track icon
            var trackSvg = await VisualElements.GetSvgAsync(SvgType.TrackOrders, 16, 16);
            await JSRuntime.InvokeVoidAsync("eval",
                $"document.querySelector('.btn-track-order .icon')?.innerHTML = `{trackSvg}`;");

            // Address icon
            var addressSvg = await VisualElements.GetSvgAsync(SvgType.Address, 24, 24);
            await JSRuntime.InvokeVoidAsync("eval",
                $"document.querySelector('.shipping-card .info-row:nth-child(1) .icon').innerHTML = `{addressSvg}`;");

            // Shipping icon
            var shippingSvg = await VisualElements.GetSvgAsync(SvgType.Order, 24, 24);
            await JSRuntime.InvokeVoidAsync("eval",
                $"document.querySelector('.shipping-card .info-row:nth-child(2) .icon').innerHTML = `{shippingSvg}`;");

            // Tracking icon
            if (!string.IsNullOrEmpty(Order?.TrackingNumber))
            {
                var trackingSvg = await VisualElements.GetSvgAsync(SvgType.Records, 24, 24);
                await JSRuntime.InvokeVoidAsync("eval",
                    $"document.querySelector('.shipping-card .info-row:nth-child(3) .icon')?.innerHTML = `{trackingSvg}`;");
            }

            // Payment icon
            var paymentSvg = await VisualElements.GetSvgAsync(SvgType.Payment, 24, 24);
            await JSRuntime.InvokeVoidAsync("eval",
                $"document.querySelector('.payment-card .info-row:nth-child(1) .icon').innerHTML = `{paymentSvg}`;");

            // Status icon
            var statusSvg = await VisualElements.GetSvgAsync(SvgType.CheckMark, 24, 24);
            await JSRuntime.InvokeVoidAsync("eval",
                $"document.querySelector('.payment-card .info-row:nth-child(2) .icon').innerHTML = `{statusSvg}`;");

            // Reference icon
            if (!string.IsNullOrEmpty(Order?.PaymentReference))
            {
                var referenceSvg = await VisualElements.GetSvgAsync(SvgType.Records, 24, 24);
                await JSRuntime.InvokeVoidAsync("eval",
                    $"document.querySelector('.payment-card .info-row:nth-child(3) .icon')?.innerHTML = `{referenceSvg}`;");
            }

            // Customer icons
            var userSvg = await VisualElements.GetSvgAsync(SvgType.User, 24, 24);
            await JSRuntime.InvokeVoidAsync("eval",
                $"document.querySelector('.customer-card .info-row:nth-child(1) .icon').innerHTML = `{userSvg}`;");

            var emailSvg = await VisualElements.GetSvgAsync(SvgType.Mail, 24, 24);
            await JSRuntime.InvokeVoidAsync("eval",
                $"document.querySelector('.customer-card .info-row:nth-child(2) .icon').innerHTML = `{emailSvg}`;");

            var phoneSvg = await VisualElements.GetSvgAsync(SvgType.Contact, 24, 24);
            await JSRuntime.InvokeVoidAsync("eval",
                $"document.querySelector('.customer-card .info-row:nth-child(3) .icon').innerHTML = `{phoneSvg}`;");

            // Action icons
            if (Order?.CanCancel == true)
            {
                var cancelSvg = await VisualElements.GetSvgAsync(SvgType.Close, 16, 16);
                await JSRuntime.InvokeVoidAsync("eval",
                    $"document.querySelector('.btn-cancel-order .icon')?.innerHTML = `{cancelSvg}`;");
            }

            if (NeedsSupportButton)
            {
                var supportSvg = await VisualElements.GetSvgAsync(SvgType.HelpCenter, 16, 16);
                await JSRuntime.InvokeVoidAsync("eval",
                    $"document.querySelector('.btn-contact-support .icon')?.innerHTML = `{supportSvg}`;");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading SVG icons");
        }
    }

    private async Task LoadOrderDetails()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Loading order details: {OrderId}",
                LogLevel.Info
            );

            Order = await OrderService.GetOrderByIdAsync(OrderId);

            if (Order == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Order not found",
                    LogLevel.Error
                );
                return;
            }

            // TODO: Load reviewed products for this user
            // ReviewedProducts = await ReviewService.GetUserReviewedProductIdsAsync(CurrentUserId);

            await MID_HelperFunctions.DebugMessageAsync(
                $"Order loaded: {Order.OrderNumber}, Status: {Order.Status}",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading order details");
            throw;
        }
    }

    private bool CanReviewProduct(string productId)
    {
        // User can review if:
        // 1. Order is delivered
        // 2. They haven't already reviewed this product
        return !ReviewedProducts.Contains(productId);
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

    private string GetPaymentStatusClass(PaymentStatus status) => status switch
    {
        PaymentStatus.Paid => "payment-paid",
        PaymentStatus.Pending => "payment-pending",
        PaymentStatus.Failed => "payment-failed",
        PaymentStatus.Refunded => "payment-refunded",
        PaymentStatus.PartiallyRefunded => "payment-partially-refunded",
        _ => ""
    };

    private void NavigateToOrders()
    {
        Navigation.NavigateTo("user/orders");
    }

    private void TrackOrder()
    {
        Navigation.NavigateTo($"user/orders/track/{OrderId}");
    }

    private void ViewProduct(string productId)
    {
        Navigation.NavigateTo($"product/{productId}");
    }

    private void ReviewProduct(string productId)
    {
        Navigation.NavigateTo($"review/create/{productId}?orderId={OrderId}");
    }

    private async Task CancelOrder()
    {
        try
        {
            var confirmed = await JSRuntime.InvokeAsync<bool>(
                "confirm", 
                "Are you sure you want to cancel this order?"
            );

            if (!confirmed) return;

            await MID_HelperFunctions.DebugMessageAsync(
                $"Cancelling order: {OrderId}",
                LogLevel.Info
            );

            var success = await OrderService.CancelOrderAsync(OrderId, CurrentUserId!);

            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Order cancelled successfully",
                    LogLevel.Info
                );

                // Reload order details
                await LoadOrderDetails();
                StateHasChanged();
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Failed to cancel order",
                    LogLevel.Error
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Cancelling order");
            Logger.LogError(ex, "Failed to cancel order: {OrderId}", OrderId);
        }
    }

    private void ContactSupport()
    {
        Navigation.NavigateTo("user/support");
    }
}
