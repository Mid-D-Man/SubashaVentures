using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SubashaVentures.Domain.Order;
using SubashaVentures.Services.Orders;
using SubashaVentures.Services.Collection;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.VisualElements;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Domain.Enums;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.User;

public partial class OrderDetails : ComponentBase
{
    [Parameter] public string OrderId { get; set; } = string.Empty;

    [Inject] private IOrderService      OrderService      { get; set; } = default!;
    [Inject] private ICollectionService CollectionService { get; set; } = default!;
    [Inject] private IPermissionService PermissionService { get; set; } = default!;
    [Inject] private IVisualElementsService VisualElements { get; set; } = default!;
    [Inject] private NavigationManager  Navigation        { get; set; } = default!;
    [Inject] private IJSRuntime         JSRuntime         { get; set; } = default!;
    [Inject] private ILogger<OrderDetails> Logger         { get; set; } = default!;

    // ── Store constants — update these to match your actual details ──────────
    private const string StorePhone   = "+234 800 000 0000";               // TODO: update
    private const string StoreEmail   = "support@mysubasha.com";
    private const string StoreHours   = "Monday – Saturday, 9:00 AM – 6:00 PM (WAT)";
    private const string StoreAddress = "SubashaVentures, Kaduna, Nigeria"; // TODO: update

    // ── State ────────────────────────────────────────────────────────────────
    private OrderViewModel? Order;
    private bool IsLoading    = true;
    private bool IsLoadingQr  = false;
    private bool _qrLoadAttempted = false;
    private string? CurrentUserId;
    private HashSet<string> ReviewedProducts = new();

    // QR code
    private string? CollectionQrUrl;
    private string  _qrSvg = string.Empty;

    // ── Computed ─────────────────────────────────────────────────────────────
    private bool IsPickupOrder =>
        Order?.ShippingMethod?.Contains("pickup", StringComparison.OrdinalIgnoreCase) == true ||
        Order?.ShippingMethod?.Contains("store",  StringComparison.OrdinalIgnoreCase) == true;

    private bool NeedsSupportButton =>
        Order != null &&
        (Order.Status == OrderStatus.Failed    ||
         Order.Status == OrderStatus.Cancelled ||
         Order.Status == OrderStatus.Delivered);

    // ── SVG icon refs ─────────────────────────────────────────────────────────
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

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        try
        {
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

        // Trigger QR load once order data is ready
        if (!_qrLoadAttempted && IsPickupOrder && Order != null && !IsLoading)
        {
            _qrLoadAttempted = true;
            await LoadQrCodeAsync();
        }
    }

    // ── Data loading ─────────────────────────────────────────────────────────

    private async Task LoadOrderDetails()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Loading order details: {OrderId}", LogLevel.Info);

            Order = await OrderService.GetOrderByIdAsync(OrderId);

            if (Order == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Order not found", LogLevel.Error);
                return;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Order loaded: {Order.OrderNumber}, Status: {Order.Status}, IsPickup: {IsPickupOrder}",
                LogLevel.Info);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading order details");
            throw;
        }
    }

    private async Task LoadQrCodeAsync()
    {
        if (!IsPickupOrder || string.IsNullOrEmpty(Order?.Id)) return;

        IsLoadingQr = true;
        StateHasChanged();

        try
        {
            CollectionQrUrl = await CollectionService.GetCollectionQrUrlAsync(Order.Id);

            if (!string.IsNullOrEmpty(CollectionQrUrl))
            {
                var module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                    "import", "./js/qrCodeModule.js");

                _qrSvg = await module.InvokeAsync<string>(
                    "generateQrCode", CollectionQrUrl, 220, "#1f2937", "#ffffff");

                await module.DisposeAsync();

                await MID_HelperFunctions.DebugMessageAsync(
                    "Collection QR code generated successfully", LogLevel.Info);
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading collection QR code");
            Logger.LogError(ex, "Failed to load QR for order {OrderId}", Order?.Id);
            _qrSvg = string.Empty; // fallback to order-number display
        }
        finally
        {
            IsLoadingQr = false;
            StateHasChanged();
        }
    }

    // ── SVG icons (existing pattern) ─────────────────────────────────────────

    private async Task LoadSvgIcons()
    {
        try
        {
            var errorSvg = await VisualElements.GetSvgAsync(SvgType.Warning, 80, 80);
            await JSRuntime.InvokeVoidAsync("eval",
                $"var el = document.querySelector('.error-icon'); if(el) el.innerHTML = `{errorSvg}`;");

            var backSvg = await VisualElements.GetSvgAsync(SvgType.Close, 20, 20);
            await JSRuntime.InvokeVoidAsync("eval",
                $"document.querySelectorAll('.btn-back .icon, .btn-back-nav .icon').forEach(el => el.innerHTML = `{backSvg}`);");

            var calendarSvg = await VisualElements.GetSvgAsync(SvgType.History, 16, 16);
            await JSRuntime.InvokeVoidAsync("eval",
                $"var el = document.querySelector('.order-date .icon'); if(el) el.innerHTML = `{calendarSvg}`;");

            var trackSvg = await VisualElements.GetSvgAsync(SvgType.TrackOrders, 16, 16);
            await JSRuntime.InvokeVoidAsync("eval",
                $"var el = document.querySelector('.btn-track-order .icon'); if(el) el.innerHTML = `{trackSvg}`;");

            var addressSvg = await VisualElements.GetSvgAsync(SvgType.Address, 24, 24);
            await JSRuntime.InvokeVoidAsync("eval",
                $"var el = document.querySelector('.shipping-card .info-row:nth-child(1) .icon'); if(el) el.innerHTML = `{addressSvg}`;");

            var shippingSvg = await VisualElements.GetSvgAsync(SvgType.Order, 24, 24);
            await JSRuntime.InvokeVoidAsync("eval",
                $"var el = document.querySelector('.shipping-card .info-row:nth-child(2) .icon'); if(el) el.innerHTML = `{shippingSvg}`;");

            if (!string.IsNullOrEmpty(Order?.TrackingNumber))
            {
                var trackingSvg = await VisualElements.GetSvgAsync(SvgType.Records, 24, 24);
                await JSRuntime.InvokeVoidAsync("eval",
                    $"var el = document.querySelector('.shipping-card .info-row:nth-child(3) .icon'); if(el) el.innerHTML = `{trackingSvg}`;");
            }

            var paymentSvg = await VisualElements.GetSvgAsync(SvgType.Payment, 24, 24);
            await JSRuntime.InvokeVoidAsync("eval",
                $"var el = document.querySelector('.payment-card .info-row:nth-child(1) .icon'); if(el) el.innerHTML = `{paymentSvg}`;");

            var statusSvg = await VisualElements.GetSvgAsync(SvgType.CheckMark, 24, 24);
            await JSRuntime.InvokeVoidAsync("eval",
                $"var el = document.querySelector('.payment-card .info-row:nth-child(2) .icon'); if(el) el.innerHTML = `{statusSvg}`;");

            if (!string.IsNullOrEmpty(Order?.PaymentReference))
            {
                var referenceSvg = await VisualElements.GetSvgAsync(SvgType.Records, 24, 24);
                await JSRuntime.InvokeVoidAsync("eval",
                    $"var el = document.querySelector('.payment-card .info-row:nth-child(3) .icon'); if(el) el.innerHTML = `{referenceSvg}`;");
            }

            var userSvg = await VisualElements.GetSvgAsync(SvgType.User, 24, 24);
            await JSRuntime.InvokeVoidAsync("eval",
                $"var el = document.querySelector('.customer-card .info-row:nth-child(1) .icon'); if(el) el.innerHTML = `{userSvg}`;");

            var emailSvg = await VisualElements.GetSvgAsync(SvgType.Mail, 24, 24);
            await JSRuntime.InvokeVoidAsync("eval",
                $"var el = document.querySelector('.customer-card .info-row:nth-child(2) .icon'); if(el) el.innerHTML = `{emailSvg}`;");

            var phoneSvg = await VisualElements.GetSvgAsync(SvgType.Contact, 24, 24);
            await JSRuntime.InvokeVoidAsync("eval",
                $"var el = document.querySelector('.customer-card .info-row:nth-child(3) .icon'); if(el) el.innerHTML = `{phoneSvg}`;");

            if (Order?.CanCancel == true)
            {
                var cancelSvg = await VisualElements.GetSvgAsync(SvgType.Close, 16, 16);
                await JSRuntime.InvokeVoidAsync("eval",
                    $"var el = document.querySelector('.btn-cancel-order .icon'); if(el) el.innerHTML = `{cancelSvg}`;");
            }

            if (NeedsSupportButton)
            {
                var supportSvg = await VisualElements.GetSvgAsync(SvgType.HelpCenter, 16, 16);
                await JSRuntime.InvokeVoidAsync("eval",
                    $"var el = document.querySelector('.btn-contact-support .icon'); if(el) el.innerHTML = `{supportSvg}`;");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading SVG icons");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool CanReviewProduct(string productId) =>
        !ReviewedProducts.Contains(productId);

    private string GetStatusClass(OrderStatus status) => status switch
    {
        OrderStatus.Pending    => "status-pending",
        OrderStatus.Processing => "status-processing",
        OrderStatus.Shipped    => "status-shipped",
        OrderStatus.Delivered  => "status-delivered",
        OrderStatus.Cancelled  => "status-cancelled",
        OrderStatus.Refunded   => "status-refunded",
        OrderStatus.Failed     => "status-failed",
        _                      => ""
    };

    private string GetPaymentStatusClass(PaymentStatus status) => status switch
    {
        PaymentStatus.Paid              => "payment-paid",
        PaymentStatus.Pending           => "payment-pending",
        PaymentStatus.Failed            => "payment-failed",
        PaymentStatus.Refunded          => "payment-refunded",
        PaymentStatus.PartiallyRefunded => "payment-partially-refunded",
        _                               => ""
    };

    // ── Navigation ────────────────────────────────────────────────────────────

    private void NavigateToOrders()    => Navigation.NavigateTo("user/orders");
    private void TrackOrder()          => Navigation.NavigateTo($"user/orders/track/{OrderId}");
    private void ViewProduct(string id)    => Navigation.NavigateTo($"product/{id}");
    private void ReviewProduct(string id)  => Navigation.NavigateTo($"review/create/{id}?orderId={OrderId}");
    private void ContactSupport()      => Navigation.NavigateTo("user/support");

    private async Task CancelOrder()
    {
        try
        {
            var confirmed = await JSRuntime.InvokeAsync<bool>(
                "confirm", "Are you sure you want to cancel this order?");

            if (!confirmed) return;

            await MID_HelperFunctions.DebugMessageAsync(
                $"Cancelling order: {OrderId}", LogLevel.Info);

            var success = await OrderService.CancelOrderAsync(OrderId, CurrentUserId!);

            if (success)
            {
                await LoadOrderDetails();
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Cancelling order");
            Logger.LogError(ex, "Failed to cancel order: {OrderId}", OrderId);
        }
    }
}
