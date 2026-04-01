// Services/Checkout/CheckoutService.cs
using SubashaVentures.Domain.Checkout;
using SubashaVentures.Domain.Cart;
using SubashaVentures.Domain.Miscellaneous;
using SubashaVentures.Domain.Order;
using SubashaVentures.Domain.User;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Services.Addresses;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Cart;
using SubashaVentures.Services.Users;
using SubashaVentures.Services.Payment;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Checkout;

public class CheckoutService : ICheckoutService
{
    private readonly IProductService _productService;
    private readonly ICartService _cartService;
    private readonly IUserService _userService;
    private readonly IAddressService _addressService;
    private readonly IPaymentService _paymentService;
    private readonly IWalletService _walletService;
    private readonly ISupabaseEdgeFunctionService _edgeFunctions;
    private readonly ILogger<CheckoutService> _logger;

    public CheckoutService(
        IProductService productService,
        ICartService cartService,
        IUserService userService,
        IAddressService addressService,
        IPaymentService paymentService,
        IWalletService walletService,
        ISupabaseEdgeFunctionService edgeFunctions,
        ILogger<CheckoutService> logger)
    {
        _productService = productService;
        _cartService = cartService;
        _userService = userService;
        _addressService = addressService;
        _paymentService = paymentService;
        _walletService = walletService;
        _edgeFunctions = edgeFunctions;
        _logger = logger;
    }

    // ==================== INITIALIZE ====================

    public async Task<CheckoutViewModel?> InitializeFromProductAsync(
        string productId,
        int quantity,
        string? size = null,
        string? color = null)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Initializing checkout from product: {productId} (Qty: {quantity}, Size: {size}, Color: {color})",
                LogLevel.Info);

            var productIdInt = int.Parse(productId);
            var product = await _productService.GetProductByIdAsync(productIdInt);

            if (product == null)
            {
                await MID_HelperFunctions.DebugMessageAsync("Product not found", LogLevel.Error);
                return null;
            }

            var variantKey = !string.IsNullOrEmpty(size) || !string.IsNullOrEmpty(color)
                ? ProductModelExtensions.BuildVariantKey(size, color)
                : null;

            var price        = product.GetVariantPrice(variantKey);
            var stock        = product.GetVariantStock(variantKey);
            var shippingCost = product.GetVariantShippingCost(variantKey);
            var imageUrl     = product.GetVariantImage(variantKey);

            if (stock < quantity)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Insufficient stock. Requested: {quantity}, Available: {stock}", LogLevel.Warning);
                return null;
            }

            string? colorHex = null;
            if (!string.IsNullOrEmpty(variantKey))
            {
                var productModel = product.ToCloudModel();
                if (productModel.Variants.TryGetValue(variantKey, out var variant))
                    colorHex = variant.ColorHex;
            }

            var checkout = new CheckoutViewModel
            {
                Items = new List<CartItemViewModel>
                {
                    new CartItemViewModel
                    {
                        ProductId      = productId,
                        Name           = product.Name,
                        Slug           = product.Slug,
                        ImageUrl       = imageUrl,
                        Price          = price,
                        OriginalPrice  = product.OriginalPrice,
                        Quantity       = quantity,
                        MaxQuantity    = stock,
                        Size           = size,
                        Color          = color,
                        ColorHex       = colorHex,
                        VariantKey     = variantKey,
                        Stock          = stock,
                        Sku            = product.Sku,
                        ShippingCost   = shippingCost,
                        HasFreeShipping = product.ToCloudModel().VariantHasFreeShipping(variantKey),
                        Weight         = product.ToCloudModel().GetVariantWeight(variantKey)
                    }
                },
                ShippingMethod = "Standard",
                ShippingCost   = shippingCost
            };

            await MID_HelperFunctions.DebugMessageAsync(
                $"Checkout initialized: Subtotal = ₦{checkout.Subtotal:N0}", LogLevel.Info);

            return checkout;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Initializing checkout from product");
            _logger.LogError(ex, "Failed to initialize checkout from product");
            return null;
        }
    }

    public async Task<CheckoutViewModel?> InitializeFromCartAsync(string userId)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Initializing checkout from cart for user: {userId}", LogLevel.Info);

            var cartSummary = await _cartService.GetCartSummaryAsync(userId);

            if (!cartSummary.Items.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync("Cart is empty", LogLevel.Warning);
                return null;
            }

            var checkout = new CheckoutViewModel
            {
                Items          = cartSummary.Items,
                ShippingMethod = "Standard",
                ShippingCost   = cartSummary.ShippingCost
            };

            await MID_HelperFunctions.DebugMessageAsync(
                $"Checkout initialized from cart: {checkout.Items.Count} items", LogLevel.Info);

            return checkout;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Initializing checkout from cart");
            _logger.LogError(ex, "Failed to initialize checkout from cart");
            return null;
        }
    }

    // ==================== SHIPPING ====================

    public async Task<List<ShippingMethodViewModel>> GetShippingMethodsAsync(
        string userId,
        List<CheckoutItemViewModel> items)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync("Calculating shipping methods", LogLevel.Info);

            decimal totalWeight = 0;
            foreach (var item in items)
            {
                var productIdInt = int.Parse(item.ProductId);
                var product = await _productService.GetProductByIdAsync(productIdInt);
                if (product != null)
                {
                    var variantKey   = ProductModelExtensions.BuildVariantKey(item.Size, item.Color);
                    var productModel = product.ToCloudModel();
                    totalWeight += productModel.GetVariantWeight(variantKey) * item.Quantity;
                }
            }

            await MID_HelperFunctions.DebugMessageAsync($"Total weight: {totalWeight}kg", LogLevel.Info);

            var shippingMethods = new List<ShippingMethodViewModel>
            {
                new ShippingMethodViewModel
                {
                    Id            = "standard",
                    Name          = "Standard Delivery",
                    Description   = "Delivery within 5-7 business days",
                    Cost          = CalculateShippingCost(totalWeight, "standard"),
                    EstimatedDays = "5-7 days",
                    Icon          = "🚚"
                },
                new ShippingMethodViewModel
                {
                    Id            = "express",
                    Name          = "Express Delivery",
                    Description   = "Delivery within 2-3 business days",
                    Cost          = CalculateShippingCost(totalWeight, "express"),
                    EstimatedDays = "2-3 days",
                    Icon          = "⚡"
                },
                new ShippingMethodViewModel
                {
                    Id            = "same-day",
                    Name          = "Same Day Delivery",
                    Description   = "Delivery within 24 hours (Kaduna only)",
                    Cost          = CalculateShippingCost(totalWeight, "same-day"),
                    EstimatedDays = "Same day",
                    Icon          = "🚀",
                    IsAvailable   = await IsSameDayAvailableAsync(userId)
                },
                new ShippingMethodViewModel
                {
                    Id            = "pickup",
                    Name          = "Store Pickup",
                    Description   = "Collect from our Kaduna store — Mon–Sat 9 AM – 6 PM",
                    Cost          = 0,
                    EstimatedDays = "Ready within 2 hours",
                    Icon          = "🏪"
                }
            };

            var available = shippingMethods.Where(m => m.IsAvailable).ToList();

            await MID_HelperFunctions.DebugMessageAsync(
                $"{available.Count} shipping methods available", LogLevel.Info);

            return available;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting shipping methods");
            return new List<ShippingMethodViewModel>();
        }
    }

    private decimal CalculateShippingCost(decimal weight, string method)
    {
        var baseRates = new Dictionary<string, decimal>
        {
            { "standard",  500  },
            { "express",   1500 },
            { "same-day",  3000 },
            { "pickup",    0    }
        };

        if (!baseRates.TryGetValue(method, out var baseRate)) return 2000;
        if (weight <= 1)  return baseRate;
        if (weight <= 5)  return baseRate + (weight - 1) * 200;
        return baseRate + 800 + (weight - 5) * 300;
    }

    private async Task<bool> IsSameDayAvailableAsync(string userId)
    {
        try
        {
            var addresses      = await _addressService.GetUserAddressesAsync(userId);
            var defaultAddress = addresses.FirstOrDefault(a => a.IsDefault);
            return defaultAddress?.State?.Contains("Kaduna", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch
        {
            return false;
        }
    }

    // ==================== PROMO CODE ====================

    public async Task<PromoCodeResult> ApplyPromoCodeAsync(string promoCode, decimal subtotal)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync($"Applying promo code: {promoCode}", LogLevel.Info);

            if (string.IsNullOrWhiteSpace(promoCode))
                return new PromoCodeResult { IsValid = false, Message = "Promo code is required", ErrorCode = "EMPTY_CODE" };

            if (promoCode.Equals("SAVE10", StringComparison.OrdinalIgnoreCase))
                return new PromoCodeResult { IsValid = true, Discount = subtotal * 0.10m, Message = "10% discount applied" };

            if (promoCode.Equals("FIRST50", StringComparison.OrdinalIgnoreCase))
                return new PromoCodeResult { IsValid = true, Discount = Math.Min(subtotal * 0.05m, 5000), Message = "₦5,000 discount applied" };

            return new PromoCodeResult { IsValid = false, Message = "Invalid or expired promo code", ErrorCode = "INVALID_CODE" };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Applying promo code");
            return new PromoCodeResult { IsValid = false, Message = "Failed to apply promo code", ErrorCode = "SYSTEM_ERROR" };
        }
    }

    // ==================== VALIDATION ====================

    public async Task<CheckoutValidationResult> ValidateCheckoutAsync(CheckoutViewModel checkout)
    {
        var result = new CheckoutValidationResult { IsValid = true };

        try
        {
            if (!checkout.Items.Any())
            {
                result.IsValid = false;
                result.Errors.Add("Cart is empty");
                return result;
            }

            if (checkout.ShippingAddress == null)
            {
                result.IsValid = false;
                result.Errors.Add("Shipping address is required");
            }

            if (checkout.PaymentMethod == PaymentMethod.Wallet)
                result.Warnings.Add("Ensure sufficient wallet balance");

            foreach (var item in checkout.Items)
            {
                var productIdInt = int.Parse(item.ProductId);
                var product      = await _productService.GetProductByIdAsync(productIdInt);

                if (product == null)
                {
                    result.IsValid = false;
                    result.Errors.Add($"{item.Name} is no longer available");
                    continue;
                }

                var variantKey   = ProductModelExtensions.BuildVariantKey(item.Size, item.Color);
                var stock        = product.GetVariantStock(variantKey);
                var currentPrice = product.GetVariantPrice(variantKey);

                if (stock < item.Quantity)
                {
                    result.IsValid = false;
                    result.Errors.Add($"{item.Name} — only {stock} left (cart has {item.Quantity})");
                }

                if (currentPrice != item.Price)
                    result.Warnings.Add($"{item.Name} price changed from ₦{item.Price:N0} to ₦{currentPrice:N0}");
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Validation: {(result.IsValid ? "PASSED" : "FAILED")} — {result.Errors.Count} errors",
                result.IsValid ? LogLevel.Info : LogLevel.Warning);

            return result;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Validating checkout");
            result.IsValid = false;
            result.Errors.Add("Validation failed");
            return result;
        }
    }

    // ==================== PLACE ORDER ====================

    public async Task<OrderPlacementResult> PlaceOrderAsync(CheckoutViewModel checkout, string userId)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync("Placing order...", LogLevel.Info);

            var validation = await ValidateCheckoutAsync(checkout);
            if (!validation.IsValid)
                return new OrderPlacementResult
                {
                    Success   = false,
                    Message   = string.Join(", ", validation.Errors),
                    ErrorCode = "VALIDATION_FAILED"
                };

            var edgeRequest = new CreateOrderEdgeRequest
            {
                UserId          = userId,
                CustomerName    = checkout.ShippingAddress?.FullName    ?? "",
                CustomerEmail   = checkout.ShippingAddress?.Email       ?? "",
                CustomerPhone   = checkout.ShippingAddress?.PhoneNumber ?? "",
                Items           = checkout.Items.Select(i => new OrderItemEdgeRequest
                {
                    ProductId   = i.ProductId,
                    ProductName = i.Name,
                    ProductSku  = i.Sku,
                    ImageUrl    = i.ImageUrl,
                    Price       = i.Price,
                    Quantity    = i.Quantity,
                    Size        = i.Size,
                    Color       = i.Color
                }).ToList(),
                Subtotal          = checkout.Subtotal,
                ShippingCost      = checkout.ShippingCost,
                Discount          = checkout.PromoDiscount,
                Tax               = 0,
                Total             = checkout.Total,
                ShippingAddressId = checkout.ShippingAddress?.Id ?? "",
                ShippingAddress   = FormatAddress(checkout.ShippingAddress),
                ShippingMethod    = checkout.ShippingMethod,
                PaymentMethod     = checkout.PaymentMethod.ToString(),
            };

            var result = await _edgeFunctions.CreateOrderAsync(edgeRequest);

            if (result.Success && result.Data != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Order created: {result.Data.OrderNumber}", LogLevel.Info);

                return new OrderPlacementResult
                {
                    Success         = true,
                    OrderId         = result.Data.OrderId,
                    OrderNumber     = result.Data.OrderNumber,
                    Message         = "Order placed successfully",
                    PaymentStatus   = checkout.PaymentMethod == Domain.Order.PaymentMethod.Wallet
                                        ? PaymentStatus.Paid
                                        : PaymentStatus.Pending,
                    CollectionQrUrl = result.Data.CollectionQrUrl,
                    IsPickup        = result.Data.IsPickup,
                };
            }

            return new OrderPlacementResult
            {
                Success   = false,
                Message   = result.Message,
                ErrorCode = result.ErrorCode ?? "ORDER_CREATION_FAILED"
            };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Placing order");
            return new OrderPlacementResult
            {
                Success   = false,
                Message   = "Failed to place order",
                ErrorCode = "SYSTEM_ERROR"
            };
        }
    }

    // ==================== PROCESS PAYMENT + CREATE ORDER ====================

    public async Task<OrderPlacementResult> ProcessPaymentAndCreateOrderAsync(
        string userId,
        CheckoutViewModel checkout,
        string paymentReference)
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Processing payment and creating order: User={userId}, Ref={paymentReference}",
                LogLevel.Info);

            if (checkout.PaymentMethod == Domain.Order.PaymentMethod.Wallet)
            {
                var hasBalance = await _walletService.HasSufficientBalanceAsync(userId, checkout.Total);
                if (!hasBalance)
                    return new OrderPlacementResult
                    {
                        Success       = false,
                        Message       = "Insufficient wallet balance",
                        ErrorCode     = "INSUFFICIENT_BALANCE",
                        PaymentStatus = PaymentStatus.Failed
                    };
            }

            var orderResult = await PlaceOrderAsync(checkout, userId);

            if (!orderResult.Success)
                return orderResult;

            if (checkout.PaymentMethod == Domain.Order.PaymentMethod.Wallet
                && !string.IsNullOrEmpty(orderResult.OrderId))
            {
                await _walletService.DeductFromWalletAsync(
                    userId,
                    checkout.Total,
                    $"Payment for order {orderResult.OrderNumber}",
                    orderResult.OrderId);

                orderResult.PaymentStatus = PaymentStatus.Paid;
            }

            return orderResult;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Processing payment and creating order");
            return new OrderPlacementResult
            {
                Success       = false,
                Message       = "Failed to process order",
                ErrorCode     = "SYSTEM_ERROR",
                PaymentStatus = PaymentStatus.Failed
            };
        }
    }

    // ==================== HELPERS ====================

    private string FormatAddress(AddressViewModel? address)
    {
        if (address == null) return "";

        var parts = new List<string>
        {
            address.AddressLine1,
            address.AddressLine2,
            address.City,
            address.State,
            address.PostalCode,
            address.Country
        };

        return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}
