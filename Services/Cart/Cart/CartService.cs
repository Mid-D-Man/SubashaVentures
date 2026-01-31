// Services/Cart/CartService.cs - FIXED VERSION with variant validation
using SubashaVentures.Models.Supabase;
using SubashaVentures.Domain.Cart;
using SubashaVentures.Services.Products;
using SubashaVentures.Utilities.HelperScripts;
using Client = Supabase.Client;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Cart;

public class CartService : ICartService
{
    private readonly Client _supabaseClient;
    private readonly IProductService _productService;
    private readonly ILogger<CartService> _logger;
    
    private Dictionary<string, int> _cartCountCache = new();

    public CartService(
        Client supabaseClient,
        IProductService productService,
        ILogger<CartService> logger)
    {
        _supabaseClient = supabaseClient;
        _productService = productService;
        _logger = logger;
    }

    /// <summary>
    /// Ensure cart row exists for user (create if missing)
    /// Race-condition safe - handles duplicate key errors gracefully
    /// </summary>
    private async Task<bool> EnsureCartExistsAsync(string userId)
    {
        try
        {
            var response = await _supabaseClient
                .From<CartModel>()
                .Where(c => c.UserId == userId)
                .Get();

            if (response?.Models == null || !response.Models.Any())
            {
                try
                {
                    var newCart = new CartModel
                    {
                        UserId = userId,
                        Items = new List<CartItem>(),
                        CreatedAt = DateTime.UtcNow
                    };

                    await _supabaseClient
                        .From<CartModel>()
                        .Insert(newCart);

                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✅ Created new cart for user: {userId}",
                        LogLevel.Info
                    );
                }
                catch (Exception insertEx)
                {
                    if (insertEx.Message.Contains("23505") || 
                        insertEx.Message.Contains("duplicate key") ||
                        insertEx.Message.Contains("cart_pkey"))
                    {
                        await MID_HelperFunctions.DebugMessageAsync(
                            $"ℹ️ Cart already exists for user (created by another request): {userId}",
                            LogLevel.Debug
                        );
                        return true;
                    }
                    
                    throw;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Ensuring cart exists");
            return false;
        }
    }

    public async Task<List<CartModel>> GetUserCartAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("GetUserCart called with empty userId");
                return new List<CartModel>();
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"🛒 Fetching cart for user: {userId}",
                LogLevel.Info
            );

            await EnsureCartExistsAsync(userId);

            var response = await _supabaseClient
                .From<CartModel>()
                .Where(c => c.UserId == userId)
                .Get();

            if (response?.Models == null || !response.Models.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No cart found after creation attempt, returning empty",
                    LogLevel.Warning
                );
                return new List<CartModel>();
            }

            var cart = response.Models.First();
            _cartCountCache[userId] = cart.Items.Sum(i => i.quantity);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Retrieved cart with {cart.Items.Count} items (total qty: {_cartCountCache[userId]})",
                LogLevel.Info
            );

            return new List<CartModel> { cart };
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting user cart");
            _logger.LogError(ex, "Failed to get cart for user: {UserId}", userId);
            return new List<CartModel>();
        }
    }

    public async Task<CartSummaryViewModel> GetCartSummaryAsync(string userId)
    {
        try
        {
            var carts = await GetUserCartAsync(userId);
            var summary = new CartSummaryViewModel();

            if (!carts.Any() || !carts[0].Items.Any())
            {
                return summary;
            }

            var cart = carts[0];
            var cartItemViewModels = new List<CartItemViewModel>();

            foreach (var cartItem in cart.Items)
            {
                var productIdInt = int.Parse(cartItem.product_id);
                var product = await _productService.GetProductByIdAsync(productIdInt);

                if (product == null)
                {
                    _logger.LogWarning("Product not found for cart item: {ProductId}", cartItem.product_id);
                    continue;
                }

                var productModel = product.ToCloudModel();
                var variantKey = ProductModelExtensions.BuildVariantKey(cartItem.size, cartItem.color);

                var price = productModel.GetVariantPrice(variantKey);
                var stock = productModel.GetVariantStock(variantKey);
                var imageUrl = productModel.GetVariantImage(variantKey);

                cartItemViewModels.Add(new CartItemViewModel
                {
                    Id = $"{cart.UserId}_{cartItem.product_id}_{cartItem.size ?? "null"}_{cartItem.color ?? "null"}",
                    ProductId = cartItem.product_id,
                    Name = product.Name,
                    Slug = product.Slug,
                    ImageUrl = imageUrl,
                    Price = price,
                    OriginalPrice = product.OriginalPrice,
                    Quantity = cartItem.quantity,
                    MaxQuantity = stock,
                    Size = cartItem.size,
                    Color = cartItem.color,
                    VariantKey = variantKey,
                    Stock = stock,
                    Sku = product.Sku,
                    AddedAt = cartItem.added_at
                });
            }

            summary.Items = cartItemViewModels;

            if (summary.Subtotal >= summary.FreeShippingThreshold)
            {
                summary.HasFreeShipping = true;
                summary.ShippingCost = 0;
            }
            else
            {
                summary.ShippingCost = 2000;
            }

            return summary;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting cart summary");
            _logger.LogError(ex, "Failed to get cart summary for user: {UserId}", userId);
            return new CartSummaryViewModel();
        }
    }

    public async Task<bool> AddToCartAsync(string userId, string productId, int quantity = 1, string? size = null, string? color = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(productId))
            {
                _logger.LogWarning("AddToCart called with empty userId or productId");
                return false;
            }

            if (quantity <= 0)
            {
                _logger.LogWarning("AddToCart called with invalid quantity: {Quantity}", quantity);
                return false;
            }

            // ✅ Normalize empty strings to null
            var normalizedSize = string.IsNullOrWhiteSpace(size) ? null : size.Trim();
            var normalizedColor = string.IsNullOrWhiteSpace(color) ? null : color.Trim();

            await MID_HelperFunctions.DebugMessageAsync(
                $"➕ Adding to cart: User={userId}, Product={productId}, Qty={quantity}, Size={normalizedSize ?? "null"}, Color={normalizedColor ?? "null"}",
                LogLevel.Info
            );

            // Get and validate product
            var productIdInt = int.Parse(productId);
            var product = await _productService.GetProductByIdAsync(productIdInt);

            if (product == null)
            {
                _logger.LogWarning("Product not found: {ProductId}", productId);
                await MID_HelperFunctions.DebugMessageAsync(
                    $"❌ Product not found: {productId}",
                    LogLevel.Error
                );
                return false;
            }

            var productModel = product.ToCloudModel();

            // ✅ CRITICAL: Validate variant selection if product has variants
            var (isValidSelection, validationError) = productModel.ValidateVariantSelection(normalizedSize, normalizedColor);
            
            if (!isValidSelection)
            {
                _logger.LogWarning(
                    "Invalid variant selection for product {ProductId}: {Error}",
                    productId,
                    validationError
                );
                await MID_HelperFunctions.DebugMessageAsync(
                    $"❌ {validationError}",
                    LogLevel.Error
                );
                return false;
            }

            // Build variant key
            var variantKey = ProductModelExtensions.BuildVariantKey(normalizedSize, normalizedColor);

            await MID_HelperFunctions.DebugMessageAsync(
                $"🔍 Variant key: '{variantKey}'",
                LogLevel.Info
            );

            // Log available variants for debugging
            if (productModel.Variants.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"📦 Product has {productModel.Variants.Count} variant(s): {string.Join(", ", productModel.Variants.Keys)}",
                    LogLevel.Info
                );
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"📦 Product has no variants. Total stock: {productModel.Stock}",
                    LogLevel.Info
                );
            }

            // Check stock
            var availableStock = productModel.GetVariantStock(variantKey);

            await MID_HelperFunctions.DebugMessageAsync(
                $"📊 Available stock for '{variantKey}': {availableStock}",
                LogLevel.Info
            );

            if (availableStock < quantity)
            {
                _logger.LogWarning(
                    "Insufficient stock for product: {ProductId}. Requested: {Quantity}, Available: {Stock}, Variant: {VariantKey}", 
                    productId, quantity, availableStock, variantKey
                );
                await MID_HelperFunctions.DebugMessageAsync(
                    $"❌ Insufficient stock: {product.Name}. Available: {availableStock}, Requested: {quantity}",
                    LogLevel.Error
                );
                return false;
            }

            // Ensure cart exists
            await EnsureCartExistsAsync(userId);

            // Call Postgres function to add to cart (use normalized values)
            var result = await _supabaseClient.Rpc<List<CartItem>>(
                "add_to_cart",
                new
                {
                    p_product_id = productId,
                    p_quantity = quantity,
                    p_size = normalizedSize,
                    p_color = normalizedColor
                }
            );

            if (result != null)
            {
                _cartCountCache.Remove(userId);

                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ Successfully added to cart! Cart now has {result.Count} unique item(s)",
                    LogLevel.Info
                );

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Adding to cart");
            _logger.LogError(ex, "Failed to add to cart: User={UserId}, Product={ProductId}", 
                userId, productId);
            return false;
        }
    }

    public async Task<bool> UpdateCartItemQuantityAsync(string userId, string cartItemId, int newQuantity)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(cartItemId))
            {
                _logger.LogWarning("UpdateCartItemQuantity called with empty parameters");
                return false;
            }

            if (newQuantity <= 0)
            {
                var parts = cartItemId.Split('_');
                if (parts.Length >= 2)
                {
                    return await RemoveFromCartAsync(userId, parts[1], 
                        parts.Length > 2 && parts[2] != "null" ? parts[2] : null, 
                        parts.Length > 3 && parts[3] != "null" ? parts[3] : null);
                }
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"🔄 Updating cart item quantity: Item={cartItemId}, NewQty={newQuantity}",
                LogLevel.Info
            );

            var idParts = cartItemId.Split('_');
            if (idParts.Length < 2)
            {
                _logger.LogWarning("Invalid cart item ID format: {CartItemId}", cartItemId);
                return false;
            }

            var productId = idParts[1];
            var itemSize = idParts.Length > 2 && idParts[2] != "null" ? idParts[2] : null;
            var itemColor = idParts.Length > 3 && idParts[3] != "null" ? idParts[3] : null;

            var productIdInt = int.Parse(productId);
            var product = await _productService.GetProductByIdAsync(productIdInt);

            if (product == null)
            {
                _logger.LogWarning("Product not found for cart update: {ProductId}", productId);
                return false;
            }

            var productModel = product.ToCloudModel();
            var variantKey = ProductModelExtensions.BuildVariantKey(itemSize, itemColor);
            var availableStock = productModel.GetVariantStock(variantKey);

            if (availableStock < newQuantity)
            {
                _logger.LogWarning(
                    "Insufficient stock for cart update. Product: {ProductId}, Requested: {Quantity}, Available: {Stock}",
                    productId, newQuantity, availableStock
                );
                await MID_HelperFunctions.DebugMessageAsync(
                    $"❌ Insufficient stock: {product.Name}. Available: {availableStock}, Requested: {newQuantity}",
                    LogLevel.Error
                );
                return false;
            }

            var response = await _supabaseClient
                .From<CartModel>()
                .Where(c => c.UserId == userId)
                .Get();

            if (response?.Models == null || !response.Models.Any())
            {
                _logger.LogWarning("Cart not found for update: {UserId}", userId);
                return false;
            }

            var cart = response.Models.First();

            var updatedItems = cart.Items.Select(item =>
            {
                if (item.product_id == productId && 
                    item.size == itemSize && 
                    item.color == itemColor)
                {
                    item.quantity = newQuantity;
                }
                return item;
            }).ToList();

            cart.Items = updatedItems;
            cart.UpdatedAt = DateTime.UtcNow;

            await cart.Update<CartModel>();

            _cartCountCache.Remove(userId);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Updated cart item quantity to {newQuantity}",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Updating cart item quantity");
            _logger.LogError(ex, "Failed to update cart item: {CartItemId}", cartItemId);
            return false;
        }
    }

    public async Task<bool> RemoveFromCartAsync(string userId, string productId, string? size = null, string? color = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(productId))
            {
                _logger.LogWarning("RemoveFromCart called with empty parameters");
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"➖ Removing from cart: User={userId}, Product={productId}, Size={size}, Color={color}",
                LogLevel.Info
            );

            await EnsureCartExistsAsync(userId);

            var result = await _supabaseClient.Rpc<List<CartItem>>(
                "remove_from_cart",
                new
                {
                    p_product_id = productId,
                    p_size = size,
                    p_color = color
                }
            );

            if (result != null)
            {
                _cartCountCache.Remove(userId);

                await MID_HelperFunctions.DebugMessageAsync(
                    "✅ Removed item from cart",
                    LogLevel.Info
                );

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Removing from cart");
            _logger.LogError(ex, "Failed to remove from cart: Product={ProductId}", productId);
            return false;
        }
    }

    public async Task<bool> ClearCartAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("ClearCart called with empty userId");
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"🗑️ Clearing cart for user: {userId}",
                LogLevel.Warning
            );

            await EnsureCartExistsAsync(userId);

            var response = await _supabaseClient
                .From<CartModel>()
                .Where(c => c.UserId == userId)
                .Get();

            if (response?.Models == null || !response.Models.Any())
            {
                return true;
            }

            var cart = response.Models.First();
            cart.Items = new List<CartItem>();
            cart.UpdatedAt = DateTime.UtcNow;

            await cart.Update<CartModel>();

            _cartCountCache.Remove(userId);

            await MID_HelperFunctions.DebugMessageAsync(
                "✅ Cart cleared",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Clearing cart");
            _logger.LogError(ex, "Failed to clear cart for user: {UserId}", userId);
            return false;
        }
    }

    public async Task<int> GetCartItemCountAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return 0;
            }

            if (_cartCountCache.TryGetValue(userId, out var cachedCount))
            {
                return cachedCount;
            }

            await EnsureCartExistsAsync(userId);

            var response = await _supabaseClient
                .From<CartModel>()
                .Where(c => c.UserId == userId)
                .Get();

            var count = response?.Models?.FirstOrDefault()?.Items.Sum(i => i.quantity) ?? 0;

            _cartCountCache[userId] = count;

            return count;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting cart count");
            return 0;
        }
    }

    public async Task<bool> RemoveFromCartByIdAsync(string userId, string cartItemId)
    {
        try
        {
            var (parsedUserId, productId, size, color) = CartItemViewModel.ParseCompositeId(cartItemId);
        
            if (parsedUserId != userId)
            {
                _logger.LogWarning("User ID mismatch in RemoveFromCartByIdAsync");
                return false;
            }
        
            return await RemoveFromCartAsync(userId, productId, size, color);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Removing from cart by ID");
            _logger.LogError(ex, "Failed to remove from cart by ID: {CartItemId}", cartItemId);
            return false;
        }
    }

    public async Task<bool> IsInCartAsync(string userId, string productId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(productId))
            {
                return false;
            }

            await EnsureCartExistsAsync(userId);

            var response = await _supabaseClient
                .From<CartModel>()
                .Where(c => c.UserId == userId)
                .Get();

            return response?.Models?.FirstOrDefault()?.Items.Any(i => i.product_id == productId) == true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Checking if in cart: {productId}");
            return false;
        }
    }

    public async Task<CartValidationResult> ValidateCartAsync(string userId)
    {
        var result = new CartValidationResult { IsValid = true };

        try
        {
            await EnsureCartExistsAsync(userId);

            var response = await _supabaseClient
                .From<CartModel>()
                .Where(c => c.UserId == userId)
                .Get();

            if (response?.Models == null || !response.Models.Any())
            {
                result.Warnings.Add("Your cart is empty");
                return result;
            }

            var cart = response.Models.First();

            if (!cart.Items.Any())
            {
                result.Warnings.Add("Your cart is empty");
                return result;
            }

            foreach (var cartItem in cart.Items)
            {
                var productIdInt = int.Parse(cartItem.product_id);
                var product = await _productService.GetProductByIdAsync(productIdInt);

                if (product == null)
                {
                    result.IsValid = false;
                    result.ItemIssues.Add(new CartItemIssue
                    {
                        CartItemId = $"{userId}_{cartItem.product_id}",
                        ProductId = cartItem.product_id,
                        IssueType = "NoLongerAvailable",
                        Message = "This product is no longer available"
                    });
                    continue;
                }

                if (!product.IsActive)
                {
                    result.IsValid = false;
                    result.ItemIssues.Add(new CartItemIssue
                    {
                        CartItemId = $"{userId}_{cartItem.product_id}",
                        ProductId = cartItem.product_id,
                        ProductName = product.Name,
                        IssueType = "NoLongerAvailable",
                        Message = $"{product.Name} is no longer available"
                    });
                }

                var productModel = product.ToCloudModel();
                var variantKey = ProductModelExtensions.BuildVariantKey(cartItem.size, cartItem.color);
                var availableStock = productModel.GetVariantStock(variantKey);

                if (availableStock < cartItem.quantity)
                {
                    result.IsValid = false;
                    result.ItemIssues.Add(new CartItemIssue
                    {
                        CartItemId = $"{userId}_{cartItem.product_id}",
                        ProductId = cartItem.product_id,
                        ProductName = product.Name,
                        IssueType = "OutOfStock",
                        Message = $"{product.Name} - Only {availableStock} available (you have {cartItem.quantity} in cart)"
                    });
                }
            }

            if (result.ItemIssues.Any())
            {
                result.Errors.Add($"{result.ItemIssues.Count} item(s) in your cart have issues");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Validating cart");
            result.IsValid = false;
            result.Errors.Add("Failed to validate cart");
        }

        return result;
    }
}
