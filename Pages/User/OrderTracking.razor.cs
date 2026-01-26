using Microsoft.AspNetCore.Components;
using SubashaVentures.Domain.Order;
using SubashaVentures.Services.Orders;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.Time;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.User;

public partial class OrderTracking : ComponentBase
{
    [Parameter] public string OrderId { get; set; } = string.Empty;

    [Inject] private IOrderService OrderService { get; set; } = default!;
    [Inject] private IPermissionService PermissionService { get; set; } = default!;
    [Inject] private IServerTimeService ServerTimeService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<OrderTracking> Logger { get; set; } = default!;

    // State
    private OrderViewModel? Order;
    private bool IsLoading = true;
    private string? CurrentUserId;
    private DateTime? EstimatedDelivery;
    private double ProgressPercentage = 0;
    private List<OrderUpdate> OrderUpdates = new();
    private string DeliveryWindowText = "";

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Ensure authenticated
            if (!await PermissionService.EnsureAuthenticatedAsync($"user/orders/{OrderId}/track"))
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

            await LoadOrderTracking();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Initializing order tracking");
            Logger.LogError(ex, "Failed to initialize order tracking");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadOrderTracking()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"📦 Loading tracking for order: {OrderId}",
                LogLevel.Info
            );

            // Load order details
            Order = await OrderService.GetOrderByIdAsync(OrderId, CurrentUserId!);

            if (Order == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ Order not found",
                    LogLevel.Error
                );
                return;
            }

            // Calculate estimated delivery if not yet delivered
            if (Order.Status != OrderStatus.Delivered && !Order.DeliveredAt.HasValue)
            {
                EstimatedDelivery = await CalculateEstimatedDelivery();
            }

            // Calculate progress percentage
            ProgressPercentage = CalculateProgressPercentage();

            // Generate order timeline
            OrderUpdates = GenerateOrderTimeline();

            // Set delivery window text
            DeliveryWindowText = GetDeliveryWindowText();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Order tracking loaded: {Order.OrderNumber}, Status: {Order.Status}, Progress: {ProgressPercentage}%",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading order tracking");
            throw;
        }
    }

    private async Task<DateTime?> CalculateEstimatedDelivery()
    {
        if (Order == null) return null;

        try
        {
            // Extract city from shipping address
            var deliveryLocation = ExtractDeliveryLocation(Order.ShippingAddress);

            await MID_HelperFunctions.DebugMessageAsync(
                $"📍 Calculating delivery for location: {deliveryLocation}",
                LogLevel.Info
            );

            // Get estimated delivery from server time service
            var estimated = await ServerTimeService.CalculateEstimatedDeliveryAsync(deliveryLocation);

            await MID_HelperFunctions.DebugMessageAsync(
                $"⏰ Estimated delivery: {estimated:yyyy-MM-dd HH:mm}",
                LogLevel.Info
            );

            return estimated;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Calculating estimated delivery");
            return null;
        }
    }

    private string ExtractDeliveryLocation(string shippingAddress)
    {
        // Try to extract city from address
        // Address format: "Street, City, State, Postal, Country"
        
        if (string.IsNullOrEmpty(shippingAddress))
            return "Abuja"; // Default

        var parts = shippingAddress.Split(',');
        
        // Check if address contains Kaduna
        if (shippingAddress.Contains("Kaduna", StringComparison.OrdinalIgnoreCase))
            return "Kaduna";
        
        // Check if address contains Abuja or FCT
        if (shippingAddress.Contains("Abuja", StringComparison.OrdinalIgnoreCase) ||
            shippingAddress.Contains("FCT", StringComparison.OrdinalIgnoreCase))
            return "Abuja";

        // Default to Abuja
        return "Abuja";
    }

    private double CalculateProgressPercentage()
    {
        if (Order == null) return 0;

        return Order.Status switch
        {
            OrderStatus.Pending => 10,
            OrderStatus.Processing => 33,
            OrderStatus.Shipped => 66,
            OrderStatus.Delivered => 100,
            OrderStatus.Cancelled => 0,
            OrderStatus.Failed => 0,
            _ => 0
        };
    }

    private List<OrderUpdate> GenerateOrderTimeline()
    {
        var updates = new List<OrderUpdate>();

        if (Order == null) return updates;

        // Order Placed
        updates.Add(new OrderUpdate
        {
            Title = "Order Placed",
            Description = $"Your order has been received and is being prepared.",
            Timestamp = Order.CreatedAt,
            Location = "Subasha Ventures",
            IsCurrent = Order.Status == OrderStatus.Pending
        });

        // Processing
        if (Order.Status == OrderStatus.Processing || 
            Order.Status == OrderStatus.Shipped || 
            Order.Status == OrderStatus.Delivered)
        {
            updates.Add(new OrderUpdate
            {
                Title = "Processing",
                Description = "Your order is being packed and prepared for shipment.",
                Timestamp = Order.CreatedAt.AddHours(2),
                Location = ExtractDeliveryLocation(Order.ShippingAddress) == "Kaduna" 
                    ? "Kaduna Warehouse" 
                    : "Abuja Distribution Center",
                IsCurrent = Order.Status == OrderStatus.Processing
            });
        }

        // Shipped
        if (Order.ShippedAt.HasValue || Order.Status == OrderStatus.Shipped || Order.Status == OrderStatus.Delivered)
        {
            updates.Add(new OrderUpdate
            {
                Title = "Shipped",
                Description = $"Your order is on its way via {Order.ShippingMethod}.",
                Timestamp = Order.ShippedAt ?? Order.CreatedAt.AddHours(4),
                Location = "In Transit",
                IsCurrent = Order.Status == OrderStatus.Shipped
            });
        }

        // Delivered
        if (Order.DeliveredAt.HasValue || Order.Status == OrderStatus.Delivered)
        {
            updates.Add(new OrderUpdate
            {
                Title = "Delivered",
                Description = "Your order has been successfully delivered!",
                Timestamp = Order.DeliveredAt ?? DateTime.UtcNow,
                Location = ExtractDeliveryLocation(Order.ShippingAddress),
                IsCurrent = Order.Status == OrderStatus.Delivered
            });
        }

        // Cancelled
        if (Order.Status == OrderStatus.Cancelled && Order.CancelledAt.HasValue)
        {
            updates.Add(new OrderUpdate
            {
                Title = "Order Cancelled",
                Description = Order.CancellationReason ?? "Order was cancelled.",
                Timestamp = Order.CancelledAt.Value,
                Location = "Subasha Ventures",
                IsCurrent = true
            });
        }

        return updates.OrderBy(u => u.Timestamp).ToList();
    }

    private string GetDeliveryWindowText()
    {
        if (Order == null) return "";

        var location = ExtractDeliveryLocation(Order.ShippingAddress);
        var hours = ServerTimeService.GetDeliveryWindowHours(location);

        if (hours == 24)
            return "Express delivery within 24 hours";
        else if (hours == 72)
            return "Standard delivery within 3 days";
        else
            return $"Delivery within {hours} hours";
    }

    private string GetTimeRemaining()
    {
        if (!EstimatedDelivery.HasValue) return "";

        var serverTime = ServerTimeService.GetCachedServerTime();
        var remaining = EstimatedDelivery.Value - serverTime;

        if (remaining.TotalHours < 0)
            return "Delivery expected soon";
        else if (remaining.TotalHours < 24)
            return $"Arriving in {remaining.TotalHours:F0} hours";
        else
            return $"Arriving in {remaining.TotalDays:F0} days";
    }

    private string GetStatusClass(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "status-pending",
        OrderStatus.Processing => "status-processing",
        OrderStatus.Shipped => "status-shipped",
        OrderStatus.Delivered => "status-delivered",
        OrderStatus.Cancelled => "status-cancelled",
        _ => ""
    };

    private void NavigateToOrders()
    {
        Navigation.NavigateTo("user/orders");
    }

    private void ContactSupport()
    {
        Navigation.NavigateTo("user/support");
    }
}

// Helper class for order timeline
public class OrderUpdate
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Location { get; set; }
    public bool IsCurrent { get; set; }
}
