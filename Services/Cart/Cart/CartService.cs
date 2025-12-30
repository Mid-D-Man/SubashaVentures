// Services/Cart/CartService.cs - UPDATED with UUID and better error handling
using SubashaVentures.Models.Supabase;
using SubashaVentures.Domain.Cart;
using SubashaVentures.Services.Products;
using SubashaVentures.Utilities.HelperScripts;
using Supabase.Postgrest;
using Client = Supabase.Client;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Cart;

public class CartService : ICartService
{
    private readonly Client _supabaseClient;
    private readonly IProductService _productService;
    private readonly ILogger<CartService> _logger;
    
    // Local cache for cart count
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
                $"Fetching cart for user: {userId}",
                LogLevel.Info
            );

            var cart = await _supabaseClient
                .From<CartModel>()
                .Where(c => c.UserId == userId)
                .Where(c => c.IsDeleted == false)
                .Order("created_at", Constants.Ordering.Descending)
                .Get();

            var items = cart?.Models ?? new List<CartModel>();

            // Update cache
            _cartCountCache[userId] = items.Sum(c => c.Quantity);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Retrieved {items.Count} cart items (total quantity: {_cartCountCache[userId]})",
                LogLevel.Info
            );

            return items;
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
            var cartItems = await GetUserCartAsync(userId);
            var summary = new CartSummaryViewModel();

            if (!cartItems.Any())
            {
                return summary;
            }

            // Fetch product details for each cart item
            var cartItemViewModels = new List<CartItemViewModel>();

            foreach (var cartItem in cartItems)
            {
                var productIdInt = int.Parse(cartItem.ProductId);
                var product = await _productService.GetProductByIdAsync(productIdInt);

                if (product == null)
                {
                    _logger.LogWarning("Product not found for cart item: {ProductId}", cartItem.ProductId);
                    continue;
                }

                cartItemViewModels.Add(new CartItemViewModel
                {
                    Id = cartItem.Id.ToString(),
                    ProductId = cartItem.ProductId,
                    Name = product.Name,
                    Slug = product.Slug,
                    ImageUrl = product.Images.FirstOrDefault() ?? "",
                    Price = product.Price,
                    OriginalPrice = product.OriginalPrice,
                    Quantity = cartItem.Quantity,
                    MaxQuantity = product.Stock,
                    Size = cartItem.Size,
                    Color = cartItem.Color,
                    Stock = product.Stock,
                    AddedAt = cartItem.CreatedAt
                });
            }

            summary.Items = cartItemViewModels;

            // Calculate shipping (free shipping over ₦50,000)
            if (summary.Subtotal >= summary.FreeShippingThreshold)
            {
                summary.HasFreeShipping = true;
                summary.ShippingCost = 0;
            }
            else
            {
                summary.ShippingCost = 2000; // Standard shipping ₦2,000
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
                $"Adding to cart: User={userId}, Product={productId}, Qty={quantity}, Size={size}, Color={color}",
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

            // Check if item already exists in cart (same product, size, color)
            var existingItems = await _supabaseClient
                .From<CartModel>()
                .Where(c => c.UserId == userId)
                .Where(c => c.ProductId == productId)
                .Where(c => c.IsDeleted == false)
                .Get();

            CartModel? existingItem = null;
            if (existingItems?.Models != null)
            {
                // Find exact match with size and color
                existingItem = existingItems.Models.FirstOrDefault(c => 
                    c.Size == size && c.Color == color);
            }

            if (existingItem != null)
            {
                // Update quantity
                existingItem.Quantity += quantity;
                existingItem.UpdatedAt = DateTime.UtcNow;
                existingItem.UpdatedBy = userId;

                // Ensure not exceeding stock
                if (existingItem.Quantity > product.Stock)
                {
                    existingItem.Quantity = product.Stock;
                }

                await existingItem.Update<CartModel>();

                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Updated cart item quantity to {existingItem.Quantity}",
                    LogLevel.Info
                );
            }
            else
            {
                // Create new cart item
                var cartItem = new CartModel
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ProductId = productId,
                    Quantity = quantity,
                    Size = size,
                    Color = color,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId,
                    IsDeleted = false
                };

                var result = await _supabaseClient
                    .From<CartModel>()
                    .Insert(cartItem);

                if (result?.Models?.Any() != true)
                {
                    _logger.LogError("Failed to insert cart item");
                    return false;
                }

                await MID_HelperFunctions.DebugMessageAsync(
                    "✓ Added new item to cart",
                    LogLevel.Info
                );
            }

            // Clear cache
            _cartCountCache.Remove(userId);

            return true;
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
                // If quantity is 0 or negative, remove the item
                return await RemoveFromCartAsync(userId, cartItemId);
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Updating cart item quantity: Item={cartItemId}, NewQty={newQuantity}",
                LogLevel.Info
            );

            if (!Guid.TryParse(cartItemId, out var cartItemGuid))
            {
                _logger.LogError("Invalid cart item ID format: {CartItemId}", cartItemId);
                return false;
            }

            var cartItem = await _supabaseClient
                .From<CartModel>()
                .Where(c => c.Id == cartItemGuid)
                .Where(c => c.UserId == userId)
                .Where(c => c.IsDeleted == false)
                .Single();

            if (cartItem == null)
            {
                _logger.LogWarning("Cart item not found: {CartItemId}", cartItemId);
                return false;
            }

            // Validate stock
            var productIdInt = int.Parse(cartItem.ProductId);
            var product = await _productService.GetProductByIdAsync(productIdInt);

            if (product == null || product.Stock < newQuantity)
            {
                _logger.LogWarning("Insufficient stock for cart update. Product: {ProductId}, Requested: {Quantity}, Available: {Stock}",
                    cartItem.ProductId, newQuantity, product?.Stock ?? 0);
                return false;
            }

            cartItem.Quantity = newQuantity;
            cartItem.UpdatedAt = DateTime.UtcNow;
            cartItem.UpdatedBy = userId;

            await cartItem.Update<CartModel>();

            // Clear cache
            _cartCountCache.Remove(userId);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Updated cart item quantity to {newQuantity}",
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

    public async Task<bool> RemoveFromCartAsync(string userId, string cartItemId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(cartItemId))
            {
                _logger.LogWarning("RemoveFromCart called with empty parameters");
                return false;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Removing from cart: Item={cartItemId}",
                LogLevel.Info
            );

            if (!Guid.TryParse(cartItemId, out var cartItemGuid))
            {
                _logger.LogError("Invalid cart item ID format: {CartItemId}", cartItemId);
                return false;
            }

            var cartItem = await _supabaseClient
                .From<CartModel>()
                .Where(c => c.Id == cartItemGuid)
                .Where(c => c.UserId == userId)
                .Where(c => c.IsDeleted == false)
                .Single();

            if (cartItem == null)
            {
                _logger.LogWarning("Cart item not found: {CartItemId}", cartItemId);
                return false;
            }

            // Soft delete
            cartItem.IsDeleted = true;
            cartItem.DeletedAt = DateTime.UtcNow;
            cartItem.DeletedBy = userId;
            cartItem.UpdatedAt = DateTime.UtcNow;
            cartItem.UpdatedBy = userId;

            await cartItem.Update<CartModel>();

            // Clear cache
            _cartCountCache.Remove(userId);

            await MID_HelperFunctions.DebugMessageAsync(
                "✓ Removed item from cart",
                LogLevel.Info
            );

            return true;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Removing from cart");
            _logger.LogError(ex, "Failed to remove from cart: {CartItemId}", cartItemId);
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
                $"Clearing cart for user: {userId}",
                LogLevel.Warning
            );

            var cartItems = await _supabaseClient
                .From<CartModel>()
                .Where(c => c.UserId == userId)
                .Where(c => c.IsDeleted == false)
                .Get();

            if (cartItems?.Models == null || !cartItems.Models.Any())
            {
                return true; // Already empty
            }

            // Soft delete all items
            foreach (var item in cartItems.Models)
            {
                item.IsDeleted = true;
                item.DeletedAt = DateTime.UtcNow;
                item.DeletedBy = userId;
                item.UpdatedAt = DateTime.UtcNow;
                item.UpdatedBy = userId;

                await item.Update<CartModel>();
            }

            // Clear cache
            _cartCountCache.Remove(userId);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Cleared {cartItems.Models.Count} cart items",
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

            var cart = await GetUserCartAsync(userId);
            var count = cart.Sum(c => c.Quantity);

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

    public async Task<bool> IsInCartAsync(string userId, string productId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(productId))
            {
                return false;
            }

            var cartItems = await _supabaseClient
                .From<CartModel>()
                .Where(c => c.UserId == userId)
                .Where(c => c.ProductId == productId)
                .Where(c => c.IsDeleted == false)
                .Get();

            return cartItems?.Models?.Any() == true;
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
            var cartItems = await GetUserCartAsync(userId);

            if (!cartItems.Any())
            {
                result.Warnings.Add("Your cart is empty");
                return result;
            }

            foreach (var cartItem in cartItems)
            {
                var productIdInt = int.Parse(cartItem.ProductId);
                var product = await _productService.GetProductByIdAsync(productIdInt);

                if (product == null)
                {
                    result.IsValid = false;
                    result.ItemIssues.Add(new CartItemIssue
                    {
                        CartItemId = cartItem.Id.ToString(),
                        ProductId = cartItem.ProductId,
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
                        CartItemId = cartItem.Id.ToString(),
                        ProductId = cartItem.ProductId,
                        ProductName = product.Name,
                        IssueType = "NoLongerAvailable",
                        Message = $"{product.Name} is no longer available"
                    });
                }

                if (product.Stock < cartItem.Quantity)
                {
                    result.IsValid = false;
                    result.ItemIssues.Add(new CartItemIssue
                    {
                        CartItemId = cartItem.Id.ToString(),
                        ProductId = cartItem.ProductId,
                        ProductName = product.Name,
                        IssueType = "OutOfStock",
                        Message = $"{product.Name} - Only {product.Stock} available (you have {cartItem.Quantity} in cart)"
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