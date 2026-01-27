// Services/Orders/OrderService.cs - FIXED with pagination and correct return types
using SubashaVentures.Domain.Order;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.SupaBase;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Postgrest;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Orders;

public class OrderService : IOrderService
{
    private readonly ISupabaseDatabaseService _database;
    private readonly ISupabaseEdgeFunctionService _edgeFunctions;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        ISupabaseDatabaseService database,
        ISupabaseEdgeFunctionService edgeFunctions,
        ILogger<OrderService> logger)
    {
        _database = database;
        _logger = logger;
        _edgeFunctions = edgeFunctions;
    }

    public async Task<bool> HasUserReceivedProductAsync(string userId, string productId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(productId))
            {
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"🔍 Checking if user {userId} received product {productId}",
                LogLevel.Info
            );

            // Parse userId to Guid
            if (!Guid.TryParse(userId, out var userGuid))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ Invalid user ID format",
                    LogLevel.Error
                );
                return false;
            }

            // Get all orders for this user using filter
            var orders = await _database.GetWithFilterAsync<OrderModel>(
                "user_id",
                Constants.Operator.Equals,
                userId  // Use string directly for UUID comparison
            );

            if (!orders.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ User has no orders",
                    LogLevel.Info
                );
                return false;
            }

            // Filter to only delivered orders
            var deliveredOrders = orders
                .Where(o => o.Status.Equals("Delivered", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!deliveredOrders.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "❌ User has no delivered orders",
                    LogLevel.Info
                );
                return false;
            }

            // Check each delivered order for the product
            foreach (var order in deliveredOrders)
            {
                var orderItems = await _database.GetWithFilterAsync<OrderItemModel>(
                    "order_id",
                    Constants.Operator.Equals,
                    order.Id.ToString()
                );

                if (orderItems.Any(item => item.ProductId == productId))
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✅ User HAS received product {productId} in order {order.OrderNumber}",
                        LogLevel.Info
                    );
                    return true;
                }
            }

            await MID_HelperFunctions.DebugMessageAsync(
                "❌ Product not found in any delivered orders",
                LogLevel.Info
            );

            return false;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Checking if user received product");
            _logger.LogError(ex, "Failed to check if user received product: User={UserId}, Product={ProductId}",
                userId, productId);
            return false;
        }
    }

    // Summary view - no pagination, returns lightweight DTOs
    public async Task<List<OrderSummaryDto>> GetUserOrdersAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new List<OrderSummaryDto>();
            }

            // Use filter with string UUID
            var orders = await _database.GetWithFilterAsync<OrderModel>(
                "user_id",
                Constants.Operator.Equals,
                userId
            );

            var summaries = new List<OrderSummaryDto>();

            foreach (var order in orders.OrderByDescending(o => o.CreatedAt))
            {
                var items = await _database.GetWithFilterAsync<OrderItemModel>(
                    "order_id",
                    Constants.Operator.Equals,
                    order.Id.ToString()
                );

                summaries.Add(OrderSummaryDto.FromCloudModel(order, items.Count));
            }

            return summaries;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting orders for user: {userId}");
            return new List<OrderSummaryDto>();
        }
    }

    // Detailed view with pagination - returns full OrderViewModel objects
    public async Task<List<OrderViewModel>> GetUserOrdersAsync(string userId, int skip = 0, int take = 100)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new List<OrderViewModel>();
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"📦 Loading orders for user: {userId} (skip: {skip}, take: {take})",
                LogLevel.Info
            );

            // Use filter with string UUID
            var orders = await _database.GetWithFilterAsync<OrderModel>(
                "user_id",
                Constants.Operator.Equals,
                userId
            );

            // Apply ordering and pagination
            var paginatedOrders = orders
                .OrderByDescending(o => o.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToList();

            var viewModels = new List<OrderViewModel>();

            foreach (var order in paginatedOrders)
            {
                var items = await _database.GetWithFilterAsync<OrderItemModel>(
                    "order_id",
                    Constants.Operator.Equals,
                    order.Id.ToString()
                );

                viewModels.Add(OrderViewModel.FromCloudModel(order, items.ToList()));
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Loaded {viewModels.Count} orders (Page skip={skip}, take={take})",
                LogLevel.Info
            );

            return viewModels;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting paginated orders for user: {userId}");
            return new List<OrderViewModel>();
        }
    }

    public async Task<OrderViewModel?> GetOrderByIdAsync(string orderId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return null;
            }

            // ✅ FIX: Use filter query instead of GetByIdAsync
            var orders = await _database.GetWithFilterAsync<OrderModel>(
                "id",
                Constants.Operator.Equals,
                orderId
            );

            var order = orders.FirstOrDefault();

            if (order == null)
            {
                return null;
            }

            var items = await _database.GetWithFilterAsync<OrderItemModel>(
                "order_id",
                Constants.Operator.Equals,
                orderId
            );

            return OrderViewModel.FromCloudModel(order, items.ToList());
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting order: {orderId}");
            return null;
        }
    }

    public async Task<OrderViewModel?> GetOrderByNumberAsync(string orderNumber)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(orderNumber))
            {
                return null;
            }

            var orders = await _database.GetWithFilterAsync<OrderModel>(
                "order_number",
                Constants.Operator.Equals,
                orderNumber
            );

            var order = orders.FirstOrDefault();

            if (order == null)
            {
                return null;
            }

            var items = await _database.GetWithFilterAsync<OrderItemModel>(
                "order_id",
                Constants.Operator.Equals,
                order.Id.ToString()
            );

            return OrderViewModel.FromCloudModel(order, items.ToList());
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting order by number: {orderNumber}");
            return null;
        }
    }

    public async Task<List<OrderSummaryDto>> GetAllOrdersAsync(int skip = 0, int take = 100)
    {
        try
        {
            var orders = await _database.GetAllAsync<OrderModel>();

            var summaries = new List<OrderSummaryDto>();

            foreach (var order in orders
                .OrderByDescending(o => o.CreatedAt)
                .Skip(skip)
                .Take(take))
            {
                var items = await _database.GetWithFilterAsync<OrderItemModel>(
                    "order_id",
                    Constants.Operator.Equals,
                    order.Id.ToString()
                );

                summaries.Add(OrderSummaryDto.FromCloudModel(order, items.Count));
            }

            return summaries;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting all orders");
            return new List<OrderSummaryDto>();
        }
    }

    public async Task<List<OrderSummaryDto>> GetOrdersByStatusAsync(OrderStatus status)
    {
        try
        {
            var orders = await _database.GetWithFilterAsync<OrderModel>(
                "status",
                Constants.Operator.Equals,
                status.ToString()
            );

            var summaries = new List<OrderSummaryDto>();

            foreach (var order in orders.OrderByDescending(o => o.CreatedAt))
            {
                var items = await _database.GetWithFilterAsync<OrderItemModel>(
                    "order_id",
                    Constants.Operator.Equals,
                    order.Id.ToString()
                );

                summaries.Add(OrderSummaryDto.FromCloudModel(order, items.Count));
            }

            return summaries;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting orders by status: {status}");
            return new List<OrderSummaryDto>();
        }
    }

    public async Task<string> CreateOrderAsync(CreateOrderRequest request)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"📦 Creating order via edge function for user: {request.UserId}",
                LogLevel.Info
            );

            // Build edge function request
            var edgeRequest = new CreateOrderEdgeRequest
            {
                UserId = request.UserId,
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
                CustomerPhone = request.CustomerPhone,
                Items = request.Items.Select(i => new OrderItemEdgeRequest
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    ProductSku = i.ProductSku,
                    ImageUrl = i.ImageUrl,
                    Price = i.Price,
                    Quantity = i.Quantity,
                    Size = i.Size,
                    Color = i.Color
                }).ToList(),
                Subtotal = request.Subtotal,
                ShippingCost = request.ShippingCost,
                Discount = request.Discount,
                Tax = request.Tax,
                Total = request.Total,
                ShippingAddressId = request.ShippingAddressId,
                ShippingAddress = request.ShippingAddress,
                ShippingMethod = request.ShippingMethod,
                PaymentMethod = request.PaymentMethod
            };

            // Call edge function
            var result = await _edgeFunctions.CreateOrderAsync(edgeRequest);

            if (result.Success && result.Data != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ Order created: {result.Data.OrderNumber}",
                    LogLevel.Info
                );

                return result.Data.OrderId;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"❌ Order creation failed: {result.Message}",
                LogLevel.Error
            );

            return string.Empty;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Creating order");
            _logger.LogError(ex, "Failed to create order");
            return string.Empty;
        }
    }

    public async Task<bool> UpdateOrderStatusAsync(string orderId, OrderStatus newStatus, string? notes = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return false;
            }

            // ✅ FIX: Use filter query instead of GetByIdAsync
            var orders = await _database.GetWithFilterAsync<OrderModel>(
                "id",
                Constants.Operator.Equals,
                orderId
            );

            var order = orders.FirstOrDefault();

            if (order == null)
            {
                return false;
            }

            order.Status = newStatus.ToString();
            order.UpdatedAt = DateTime.UtcNow;

            if (newStatus == OrderStatus.Shipped && order.ShippedAt == null)
            {
                order.ShippedAt = DateTime.UtcNow;
            }

            if (newStatus == OrderStatus.Delivered && order.DeliveredAt == null)
            {
                order.DeliveredAt = DateTime.UtcNow;
            }

            if (newStatus == OrderStatus.Cancelled && order.CancelledAt == null)
            {
                order.CancelledAt = DateTime.UtcNow;
                order.CancellationReason = notes;
            }

            if (!string.IsNullOrEmpty(notes))
            {
                order.Notes = notes;
            }

            var result = await _database.UpdateAsync(order);

            return result != null && result.Any();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Updating order status: {orderId}");
            return false;
        }
    }

    public async Task<bool> CancelOrderAsync(string orderId, string cancellationReason)
    {
        return await UpdateOrderStatusAsync(orderId, OrderStatus.Cancelled, cancellationReason);
    }

    public async Task<OrderStatistics> GetOrderStatisticsAsync()
    {
        try
        {
            var orders = await _database.GetAllAsync<OrderModel>();

            var stats = new OrderStatistics
            {
                TotalOrders = orders.Count,
                PendingOrders = orders.Count(o => o.Status == "Pending"),
                ProcessingOrders = orders.Count(o => o.Status == "Processing"),
                ShippedOrders = orders.Count(o => o.Status == "Shipped"),
                DeliveredOrders = orders.Count(o => o.Status == "Delivered"),
                CancelledOrders = orders.Count(o => o.Status == "Cancelled"),
                TotalRevenue = orders
                    .Where(o => o.Status == "Delivered")
                    .Sum(o => o.Total),
                AverageOrderValue = orders.Any()
                    ? orders.Average(o => o.Total)
                    : 0
            };

            return stats;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting order statistics");
            return new OrderStatistics();
        }
    }

    private string GenerateOrderNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var random = new Random().Next(1000, 9999);
        return $"ORD-{timestamp}-{random}";
    }
}