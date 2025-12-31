// Services/Cart/CartService.cs - UPDATED FOR JSONB DESIGN
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

            var cart = await _supabaseClient
                .From<CartModel>()
                .Where(c => c.UserId == userId)
                .Single();

            if (cart == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No cart found, returning empty",
                    LogLevel.Info
                );
                return new List<CartModel>();
            }

            // Update cache
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

                cartItemViewModels.Add(new CartItemViewModel
                {
                    Id = $"{cart.UserId}_{cartItem.product_id}_{cartItem.size}_{cartItem.color}",
                    ProductId = cartItem.product_id,
                    Name = product.Name,
                    Slug = product.Slug,
                    ImageUrl = product.Images.FirstOrDefault() ?? "",
                    Price = product.Price,
                    OriginalPrice = product.OriginalPrice,
                    Quantity = cartItem.quantity,
                    MaxQuantity = product.Stock,
                    Size = cartItem.size,
                    Color = cartItem.color,
                    Stock = product.Stock,
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

            await MID_HelperFunctions.DebugMessageAsync(
                $"➕ Adding to cart: User={userId}, Product={productId}, Qty={quantity}, Size={size}, Color={color}",
                LogLevel.Info
            );

            // Validate product exists and has stock
            var productIdInt = int.Parse(productId);
            var product = await _productService.GetProductByIdAsync(productIdInt);

            if (product == null)
            {
                _logger.LogWarning("Product not found: {ProductId}", productId);
                return false;
            }

            if (product.Stock < quantity)
            {
                _logger.LogWarning("Insufficient stock for product: {ProductId}. Requested: {Quantity}, Available: {Stock}", 
                    productId, quantity, product.Stock);
                return false;
            }

            // Call Postgres function to add to cart
            var result = await _supabaseClient.Rpc<List<CartItem>>(
                "add_to_cart",
                new
                {
                    p_product_id = productId,
                    p_quantity = quantity,
                    p_size = size,
                    p_color = color
                }
            );

            if (result != null)
            {
                // Clear cache
                _cartCountCache.Remove(userId);

                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ Successfully added to cart! Cart now has {result.Count} items",
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
                // Parse cartItemId to extract product info
                var parts = cartItemId.Split('_');
                if (parts.Length >= 2)
                {
                    return await RemoveFromCartAsync(userId, parts[1], 
                        parts.Length > 2 ? parts[2] : null, 
                        parts.Length > 3 ? parts[3] : null);
                }
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"🔄 Updating cart item quantity: Item={cartItemId}, NewQty={newQuantity}",
                LogLevel.Info
            );

            // Get current cart
            var cart = await _supabaseClient
                .From<CartModel>()
                .Where(c => c.UserId == userId)
                .Single();

            if (cart == null)
            {
                _logger.LogWarning("Cart not found for update: {UserId}", userId);
                return false;
            }

            // Parse cartItemId (format: userId_productId_size_color)
            var idParts = cartItemId.Split('_');
            if (idParts.Length < 2)
            {
                _logger.LogWarning("Invalid cart item ID format: {CartItemId}", cartItemId);
                return false;
            }

            var productId = idParts[1];
            var itemSize = idParts.Length > 2 && idParts[2] != "null" ? idParts[2] : null;
            var itemColor = idParts.Length > 3 && idParts[3] != "null" ? idParts[3] : null;

            // Validate stock
            var productIdInt = int.Parse(productId);
            var product = await _productService.GetProductByIdAsync(productIdInt);

            if (product == null || product.Stock < newQuantity)
            {
                _logger.LogWarning("Insufficient stock for cart update. Product: {ProductId}, Requested: {Quantity}, Available: {Stock}",
                    productId, newQuantity, product?.Stock ?? 0);
                return false;
            }

            // Update the specific item's quantity
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

            // Clear cache
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

            // Call Postgres function to remove from cart
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
                // Clear cache
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

            var cart = await _supabaseClient
                .From<CartModel>()
                .Where(c => c.UserId == userId)
                .Single();

            if (cart == null)
            {
                return true; // Already empty
            }

            cart.Items = new List<CartItem>();
            cart.UpdatedAt = DateTime.UtcNow;

            await cart.Update<CartModel>();

            // Clear cache
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

            // Check cache first
            if (_cartCountCache.TryGetValue(userId, out var cachedCount))
            {
                return cachedCount;
            }

            var cart = await _supabaseClient
                .From<CartModel>()
                .Where(c => c.UserId == userId)
                .Single();

            var count = cart?.Items.Sum(i => i.quantity) ?? 0;

            // Update cache
            _cartCountCache[userId] = count;

            return count;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting cart count");
            return 0;
        }
    }
    /// <summary>
    /// Remove item from cart using composite ID (backward compatibility)
    /// </summary>
    public async Task<bool> RemoveFromCartByIdAsync(string userId, string cartItemId)
    {
        try
        {
            // Parse composite ID
            var (parsedUserId, productId, size, color) = CartItemViewModel.ParseCompositeId(cartItemId);
        
            // Verify userId matches
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

            var cart = await _supabaseClient
                .From<CartModel>()
                .Where(c => c.UserId == userId)
                .Single();

            return cart?.Items.Any(i => i.product_id == productId) == true;
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
            var cart = await _supabaseClient
                .From<CartModel>()
                .Where(c => c.UserId == userId)
                .Single();

            if (cart == null || !cart.Items.Any())
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

                if (product.Stock < cartItem.quantity)
                {
                    result.IsValid = false;
                    result.ItemIssues.Add(new CartItemIssue
                    {
                        CartItemId = $"{userId}_{cartItem.product_id}",
                        ProductId = cartItem.product_id,
                        ProductName = product.Name,
                        IssueType = "OutOfStock",
                        Message = $"{product.Name} - Only {product.Stock} available (you have {cartItem.quantity} in cart)"
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