// Pages/Admin/OrderManagement.razor.cs
using Microsoft.AspNetCore.Components;
using SubashaVentures.Domain.Order;
using SubashaVentures.Services.Orders;
using SubashaVentures.Services.Time;
using SubashaVentures.Components.Shared.Notifications;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Admin;

public partial class OrderManagement : ComponentBase
{
    [Inject] private IOrderService OrderService { get; set; } = default!;
    [Inject] private IServerTimeService ServerTimeService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    // Component References
    private NotificationComponent? notificationComponent;
    private DynamicModal? orderDetailsModal;
    private DynamicModal? updateStatusModal;
    private ConfirmationPopup? cancelOrderPopup;

    // State
    private List<OrderViewModel> allOrders = new();
    private List<OrderViewModel> filteredOrders = new();
    private List<OrderViewModel> paginatedOrders = new();
    private HashSet<string> selectedOrders = new();
    
    // Loading states
    private bool isLoading = true;
    private bool isRefreshing = false;
    private bool isSaving = false;

    // Filters and search
    private string searchQuery = "";
    private string statusFilter = "";
    private string paymentFilter = "";
    private string sortOrder = "newest";
    private string viewMode = "list";

    // Pagination
    private int currentPage = 1;
    private int pageSize = 20;
    private int totalPages => (int)Math.Ceiling(filteredOrders.Count / (double)pageSize);

    // Stats
    private int totalOrders = 0;
    private int pendingOrders = 0;
    private int processingOrders = 0;
    private int shippedOrders = 0;
    private int deliveredOrders = 0;
    private decimal totalRevenue = 0;

    // Server time
    private DateTime currentServerTime = DateTime.UtcNow;

    // Modal states - Order Details
    private OrderViewModel? selectedOrderForDetails = null;
    private bool isOrderDetailsModalOpen = false;

    // Modal states - Update Status
    private OrderViewModel? selectedOrderForStatusUpdate = null;
    private bool isUpdateStatusModalOpen = false;
    private string newStatus = "";
    private string trackingNumber = "";
    private string courierName = "";
    private string statusUpdateNotes = "";

    // Modal states - Cancel Order
    private OrderViewModel? selectedOrderForCancellation = null;
    private bool isCancelOrderModalOpen = false;
    private string cancellationReason = "";

    protected override async Task OnInitializedAsync()
    {
        await MID_HelperFunctions.DebugMessageAsync("📦 Initializing Order Management page", LogLevel.Info);
        
        try
        {
            // Sync server time
            currentServerTime = await ServerTimeService.GetCurrentServerTimeAsync();
            
            // Load orders
            await LoadOrdersAsync();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Initializing Order Management");
            notificationComponent?.ShowError("Failed to load orders. Please refresh the page.");
        }
        finally
        {
            isLoading = false;
        }
    }

    // ===== DATA LOADING =====

    private async Task LoadOrdersAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync("📥 Loading all orders", LogLevel.Info);

            // Get all order summaries first
            var summaries = await OrderService.GetAllOrdersAsync(0, 200);
            
            // Load full details for orders
            allOrders = new List<OrderViewModel>();
            foreach (var summary in summaries)
            {
                var orderDetails = await OrderService.GetOrderByIdAsync(summary.Id);
                if (orderDetails != null)
                {
                    allOrders.Add(orderDetails);
                }
            }

            await MID_HelperFunctions.DebugMessageAsync($"✅ Loaded {allOrders.Count} orders", LogLevel.Info);

            // Calculate stats
            CalculateStats();

            // Apply filters
            ApplyFilters();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading orders");
            throw;
        }
    }

    private void CalculateStats()
    {
        totalOrders = allOrders.Count;
        pendingOrders = allOrders.Count(o => o.Status == OrderStatus.Pending);
        processingOrders = allOrders.Count(o => o.Status == OrderStatus.Processing);
        shippedOrders = allOrders.Count(o => o.Status == OrderStatus.Shipped);
        deliveredOrders = allOrders.Count(o => o.Status == OrderStatus.Delivered);
        totalRevenue = allOrders
            .Where(o => o.Status == OrderStatus.Delivered && o.PaymentStatus == PaymentStatus.Paid)
            .Sum(o => o.Total);
    }

    // ===== FILTERS AND SEARCH =====

    private void ApplyFilters()
{
    // Fix: Convert to List immediately
    filteredOrders = allOrders.ToList();

    // Search filter
    if (!string.IsNullOrWhiteSpace(searchQuery))
    {
        var query = searchQuery.ToLower();
        filteredOrders = filteredOrders.Where(o =>
            o.OrderNumber.ToLower().Contains(query) ||
            o.CustomerName.ToLower().Contains(query) ||
            o.CustomerEmail.ToLower().Contains(query) ||
            o.CustomerPhone.Contains(query)
        ).ToList();
    }

    // Status filter
    if (!string.IsNullOrWhiteSpace(statusFilter))
    {
        if (Enum.TryParse<OrderStatus>(statusFilter, out var status))
        {
            filteredOrders = filteredOrders.Where(o => o.Status == status).ToList();
        }
    }

    // Payment filter
    if (!string.IsNullOrWhiteSpace(paymentFilter))
    {
        if (Enum.TryParse<PaymentStatus>(paymentFilter, out var paymentStatus))
        {
            filteredOrders = filteredOrders.Where(o => o.PaymentStatus == paymentStatus).ToList();
        }
    }

    // Sort
    filteredOrders = sortOrder switch
    {
        "oldest" => filteredOrders.OrderBy(o => o.CreatedAt).ToList(),
        "amount-high" => filteredOrders.OrderByDescending(o => o.Total).ToList(),
        "amount-low" => filteredOrders.OrderBy(o => o.Total).ToList(),
        "pending-delivery" => filteredOrders
            .Where(o => o.Status == OrderStatus.Shipped || o.Status == OrderStatus.Processing)
            .OrderBy(o => GetEstimatedDeliveryTime(o))
            .ToList(),
        _ => filteredOrders.OrderByDescending(o => o.CreatedAt).ToList(), // newest (default)
    };

    // Paginate
    ApplyPagination();
}

    private void ApplyPagination()
    {
        paginatedOrders = filteredOrders
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    private void HandleSearch()
    {
        currentPage = 1;
        ApplyFilters();
        StateHasChanged();
    }

    private void HandleStatusFilter(ChangeEventArgs e)
    {
        statusFilter = e.Value?.ToString() ?? "";
        currentPage = 1;
        ApplyFilters();
        StateHasChanged();
    }

    private void HandlePaymentFilter(ChangeEventArgs e)
    {
        paymentFilter = e.Value?.ToString() ?? "";
        currentPage = 1;
        ApplyFilters();
        StateHasChanged();
    }

    private void HandleSortChange(ChangeEventArgs e)
    {
        sortOrder = e.Value?.ToString() ?? "newest";
        ApplyFilters();
        StateHasChanged();
    }

    // ===== SELECTION =====

    private void HandleSelectAll(ChangeEventArgs e)
    {
        if ((bool)e.Value)
        {
            selectedOrders = paginatedOrders.Select(o => o.Id).ToHashSet();
        }
        else
        {
            selectedOrders.Clear();
        }
        StateHasChanged();
    }

    private void HandleSelectionChanged(string orderId, bool isSelected)
    {
        if (isSelected)
        {
            selectedOrders.Add(orderId);
        }
        else
        {
            selectedOrders.Remove(orderId);
        }
        StateHasChanged();
    }

    // ===== REFRESH =====

    private async Task RefreshOrders()
    {
        isRefreshing = true;
        StateHasChanged();

        try
        {
            // Re-sync server time
            await ServerTimeService.ForceSyncAsync();
            currentServerTime = ServerTimeService.GetCachedServerTime();

            // Reload orders
            await LoadOrdersAsync();

            notificationComponent?.ShowSuccess("Orders refreshed successfully!");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Refreshing orders");
            notificationComponent?.ShowError("Failed to refresh orders");
        }
        finally
        {
            isRefreshing = false;
            StateHasChanged();
        }
    }

    // ===== ORDER DETAILS MODAL =====

    private void OpenOrderDetailsModal(OrderViewModel order)
    {
        selectedOrderForDetails = order;
        isOrderDetailsModalOpen = true;
        StateHasChanged();
    }

    private void CloseOrderDetailsModal()
    {
        selectedOrderForDetails = null;
        isOrderDetailsModalOpen = false;
        StateHasChanged();
    }

    // ===== UPDATE STATUS MODAL =====

    private void OpenUpdateStatusModal(OrderViewModel order)
    {
        selectedOrderForStatusUpdate = order;
        newStatus = "";
        trackingNumber = order.TrackingNumber ?? "";
        courierName = order.CourierName ?? "";
        statusUpdateNotes = "";
        isUpdateStatusModalOpen = true;
        
        // Close details modal if open
        isOrderDetailsModalOpen = false;
        
        StateHasChanged();
    }

    private void CloseUpdateStatusModal()
    {
        selectedOrderForStatusUpdate = null;
        isUpdateStatusModalOpen = false;
        newStatus = "";
        trackingNumber = "";
        courierName = "";
        statusUpdateNotes = "";
        StateHasChanged();
    }

    private async Task HandleUpdateStatus()
    {
        if (selectedOrderForStatusUpdate == null || string.IsNullOrEmpty(newStatus))
        {
            notificationComponent?.ShowWarning("Please select a status");
            return;
        }

        if (!Enum.TryParse<OrderStatus>(newStatus, out var status))
        {
            notificationComponent?.ShowError("Invalid status selected");
            return;
        }

        isSaving = true;
        StateHasChanged();

        try
        {
            // Update status
            var success = await OrderService.UpdateOrderStatusAsync(
                selectedOrderForStatusUpdate.Id, 
                status, 
                statusUpdateNotes
            );

            if (success)
            {
                // If shipped, update tracking info if provided
                if (status == OrderStatus.Shipped)
                {
                    // Note: You might need to add a separate method to update tracking info
                    // For now, we'll include it in the notes
                    if (!string.IsNullOrEmpty(trackingNumber))
                    {
                        await MID_HelperFunctions.DebugMessageAsync(
                            $"Tracking: {trackingNumber}, Courier: {courierName}", 
                            LogLevel.Info
                        );
                    }
                }

                notificationComponent?.ShowSuccess($"Order status updated to {status}");
                
                // Reload orders
                await LoadOrdersAsync();
                
                CloseUpdateStatusModal();
            }
            else
            {
                notificationComponent?.ShowError("Failed to update order status");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Updating order status");
            notificationComponent?.ShowError("An error occurred while updating the order");
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    // ===== CANCEL ORDER MODAL =====

    private void OpenCancelOrderModal(OrderViewModel order)
    {
        selectedOrderForCancellation = order;
        cancellationReason = "";
        isCancelOrderModalOpen = true;
        
        // Close details modal if open
        isOrderDetailsModalOpen = false;
        
        StateHasChanged();
    }

    private void CloseCancelOrderModal()
    {
        selectedOrderForCancellation = null;
        isCancelOrderModalOpen = false;
        cancellationReason = "";
        StateHasChanged();
    }

    private async Task HandleCancelOrder()
    {
        if (selectedOrderForCancellation == null)
            return;

        if (string.IsNullOrWhiteSpace(cancellationReason))
        {
            notificationComponent?.ShowWarning("Please provide a cancellation reason");
            return;
        }

        isSaving = true;
        StateHasChanged();

        try
        {
            var success = await OrderService.CancelOrderAsync(
                selectedOrderForCancellation.Id, 
                cancellationReason
            );

            if (success)
            {
                notificationComponent?.ShowSuccess("Order cancelled successfully");
                
                // Reload orders
                await LoadOrdersAsync();
                
                CloseCancelOrderModal();
            }
            else
            {
                notificationComponent?.ShowError("Failed to cancel order");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Cancelling order");
            notificationComponent?.ShowError("An error occurred while cancelling the order");
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    // ===== BULK ACTIONS =====

    private async Task HandleBulkMarkShipped()
    {
        if (!selectedOrders.Any()) return;

        isSaving = true;
        StateHasChanged();

        try
        {
            int successCount = 0;
            foreach (var orderId in selectedOrders)
            {
                var success = await OrderService.UpdateOrderStatusAsync(orderId, OrderStatus.Shipped);
                if (success) successCount++;
            }

            notificationComponent?.ShowSuccess($"Marked {successCount} order(s) as shipped");
            selectedOrders.Clear();
            await LoadOrdersAsync();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Bulk marking orders as shipped");
            notificationComponent?.ShowError("Failed to update orders");
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    private async Task HandleBulkMarkDelivered()
    {
        if (!selectedOrders.Any()) return;

        isSaving = true;
        StateHasChanged();

        try
        {
            int successCount = 0;
            foreach (var orderId in selectedOrders)
            {
                var success = await OrderService.UpdateOrderStatusAsync(orderId, OrderStatus.Delivered);
                if (success) successCount++;
            }

            notificationComponent?.ShowSuccess($"Marked {successCount} order(s) as delivered");
            selectedOrders.Clear();
            await LoadOrdersAsync();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Bulk marking orders as delivered");
            notificationComponent?.ShowError("Failed to update orders");
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    // ===== EXPORT =====

    private async Task HandleExport()
    {
        notificationComponent?.ShowInfo("Export functionality coming soon!");
        await Task.CompletedTask;
    }

    // ===== DELIVERY TIME CALCULATIONS =====

    private TimeSpan GetEstimatedDeliveryTime(OrderViewModel order)
    {
        // Calculate based on shipping location and current time
        var deliveryLocation = ExtractCityFromAddress(order.ShippingAddress);
        var deliveryHours = ServerTimeService.GetDeliveryWindowHours(deliveryLocation);
        var estimatedDeliveryTime = order.CreatedAt.AddHours(deliveryHours);
        
        return estimatedDeliveryTime - currentServerTime;
    }

    private bool IsOrderUrgent(OrderViewModel order)
    {
        if (order.Status != OrderStatus.Shipped && order.Status != OrderStatus.Processing)
            return false;

        var timeLeft = GetEstimatedDeliveryTime(order);
        return timeLeft.TotalHours < 24 && timeLeft.TotalHours > 0;
    }

    private string FormatTimeLeft(TimeSpan timeLeft)
    {
        if (timeLeft.TotalHours < 0)
            return "⚠️ Overdue";
        
        if (timeLeft.TotalDays >= 1)
            return $"{(int)timeLeft.TotalDays}d {timeLeft.Hours}h";
        
        if (timeLeft.TotalHours >= 1)
            return $"{(int)timeLeft.TotalHours}h {timeLeft.Minutes}m";
        
        return $"{timeLeft.Minutes}m";
    }

    private string ExtractCityFromAddress(string address)
    {
        // Simple extraction - assumes format contains city name
        var parts = address.Split(',');
        if (parts.Length >= 2)
        {
            return parts[^2].Trim(); // Second to last part is usually the city
        }
        return "Abuja"; // Default
    }

    // ===== PAGINATION =====

    private void PreviousPage()
    {
        if (currentPage > 1)
        {
            currentPage--;
            ApplyPagination();
            StateHasChanged();
        }
    }

    private void NextPage()
    {
        if (currentPage < totalPages)
        {
            currentPage++;
            ApplyPagination();
            StateHasChanged();
        }
    }

    private void GoToPage(int page)
    {
        currentPage = page;
        ApplyPagination();
        StateHasChanged();
    }
}
