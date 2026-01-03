// Services/Orders/OrderService.cs
using SubashaVentures.Domain.Order;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Services.SupaBase;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Postgrest;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Orders;

public class OrderService : IOrderService
{
    private readonly ISupabaseDatabaseService _database;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        ISupabaseDatabaseService database,
        ILogger<OrderService> logger)
    {
        _database = database;
        _logger = logger;
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

            // Get all delivered orders for this user
            var orders = await _database.GetWithFilterAsync<OrderModel>(
                "user_id",
                Constants.Operator.Equals,
                userId
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

    public async Task<List<OrderSummaryDto>> GetUserOrdersAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new List<OrderSummaryDto>();
            }

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

    public async Task<OrderViewModel?> GetOrderByIdAsync(string orderId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(orderId) || !Guid.TryParse(orderId, out var orderGuid))
            {
                return null;
            }

            var order = await _database.GetByIdAsync<OrderModel>(orderGuid);

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
            var now = DateTime.UtcNow;
            var orderNumber = GenerateOrderNumber();

            var orderModel = new OrderModel
            {
                Id = Guid.NewGuid(),
                OrderNumber = orderNumber,
                UserId = request.UserId,
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
                CustomerPhone = request.CustomerPhone,
                Subtotal = request.Subtotal,
                ShippingCost = request.ShippingCost,
                Discount = request.Discount,
                Tax = request.Tax,
                Total = request.Total,
                ShippingAddressId = request.ShippingAddressId,
                ShippingAddressSnapshot = request.ShippingAddress,
                ShippingMethod = request.ShippingMethod,
                PaymentMethod = request.PaymentMethod,
                PaymentStatus = "Pending",
                Status = "Pending",
                CreatedAt = now,
                CreatedBy = request.UserId
            };

            var result = await _database.InsertAsync(orderModel);

            if (result == null || !result.Any())
            {
                return string.Empty;
            }

            var createdOrder = result.First();

            // Create order items
            foreach (var itemReq in request.Items)
            {
                var orderItem = new OrderItemModel
                {
                    Id = Guid.NewGuid(),
                    OrderId = createdOrder.Id,
                    ProductId = itemReq.ProductId,
                    ProductName = itemReq.ProductName,
                    ProductSku = itemReq.ProductSku,
                    ImageUrl = itemReq.ImageUrl,
                    Price = itemReq.Price,
                    Quantity = itemReq.Quantity,
                    Size = itemReq.Size,
                    Color = itemReq.Color,
                    Subtotal = itemReq.Price * itemReq.Quantity,
                    CreatedAt = now,
                    CreatedBy = request.UserId
                };

                await _database.InsertAsync(orderItem);
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Order created: {orderNumber}",
                LogLevel.Info
            );

            return createdOrder.Id.ToString();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Creating order");
            return string.Empty;
        }
    }

    public async Task<bool> UpdateOrderStatusAsync(string orderId, OrderStatus newStatus, string? notes = null)
    {
        try
        {
            if (!Guid.TryParse(orderId, out var orderGuid))
            {
                return false;
            }

            var order = await _database.GetByIdAsync<OrderModel>(orderGuid);

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
