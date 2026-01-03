// Pages/User/WishlistCart.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Domain.Cart;
using SubashaVentures.Domain.User;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.Cart;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Wishlist;
using SubashaVentures.Utilities.ObjectPooling;
using System.Security.Claims;

namespace SubashaVentures.Pages.User;

public partial class WishlistCart : IDisposable
{
    [Inject] private IWishlistService WishlistService { get; set; } = default!;
    [Inject] private ICartService CartService { get; set; } = default!;
    [Inject] private IProductService ProductService { get; set; } = default!;
    [Inject] private IPermissionService PermissionService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    // Object pool for reusable components
    private static readonly MID_ComponentObjectPool<Dictionary<string, bool>> SelectionDictPool =
        new(() => new Dictionary<string, bool>(), dict => dict.Clear(), maxPoolSize: 50);

    // State
    private TabType ActiveTab = TabType.Wishlist;
    private bool IsLoading = true;
    private string? CurrentUserId;

    // Wishlist
    private List<WishlistItemViewModel> WishlistItems = new();
    private Dictionary<string, bool> SelectedWishlistItems = new();
    private int CurrentWishlistPage = 1;
    private const int WishlistItemsPerPage = 12;

    // Cart
    private List<CartItemViewModel> CartItems = new();
    private CartSummaryViewModel CartSummary = new();
    private int CurrentCartPage = 1;
    private const int CartItemsPerPage = 10;
    private CartValidationResult? ValidationResult;

    // Promo
    private string PromoCodeInput = "";
    private string PromoMessage = "";
    private string PromoMessageType = "";
    private bool IsApplyingPromo = false;

    // Confirmation
    private ConfirmationPopup? ConfirmationPopup;
    private string ConfirmationTitle = "";
    private string ConfirmationMessage = "";
    private string ConfirmButtonText = "Confirm";
    private Action? PendingAction;

    // Computed Properties
    private int WishlistCount => WishlistItems.Count;
    private int CartCount => CartItems.Sum(i => i.Quantity);
    private int TotalWishlistItems => WishlistItems.Count;
    private int TotalWishlistPages => (int)Math.Ceiling(TotalWishlistItems / (double)WishlistItemsPerPage);
    private int TotalCartPages => (int)Math.Ceiling(CartItems.Count / (double)CartItemsPerPage);
    private bool CanCheckout => CartSummary.CanCheckout && ValidationResult?.IsValid != false;

    private IEnumerable<WishlistItemViewModel> PaginatedWishlistItems =>
        WishlistItems
            .Skip((CurrentWishlistPage - 1) * WishlistItemsPerPage)
            .Take(WishlistItemsPerPage);

    private IEnumerable<CartItemViewModel> PaginatedCartItems =>
        CartItems
            .Skip((CurrentCartPage - 1) * CartItemsPerPage)
            .Take(CartItemsPerPage);

    protected override async Task OnInitializedAsync()
    {
        await GetCurrentUserId();
        await LoadData();
    }

    private async Task GetCurrentUserId()
    {
        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            CurrentUserId = authState.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? authState.User?.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(CurrentUserId))
            {
                Console.WriteLine("❌ User not authenticated");
                Navigation.NavigateTo("/signin?returnUrl=/user/wishlist-cart");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error getting user ID: {ex.Message}");
        }
    }

    private async Task LoadData()
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return;
        }

        IsLoading = true;
        StateHasChanged();

        try
        {
            if (ActiveTab == TabType.Wishlist)
            {
                await LoadWishlist();
            }
            else
            {
                await LoadCart();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error loading data: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadWishlist()
    {
        try
        {
            var wishlists = await WishlistService.GetUserWishlistAsync(CurrentUserId!);
            WishlistItems.Clear();

            if (!wishlists.Any() || !wishlists[0].Items.Any())
            {
                Console.WriteLine("✅ Wishlist is empty");
                return;
            }

            var wishlist = wishlists[0];
            Console.WriteLine($"📦 Loading {wishlist.Items.Count} wishlist items");

            foreach (var item in wishlist.Items)
            {
                try
                {
                    var productIdInt = int.Parse(item.product_id);
                    var product = await ProductService.GetProductByIdAsync(productIdInt);

                    if (product == null)
                    {
                        Console.WriteLine($"⚠️ Product not found: {item.product_id}");
                        continue;
                    }

                    WishlistItems.Add(new WishlistItemViewModel
                    {
                        Id = $"{CurrentUserId}_{item.product_id}",
                        ProductId = item.product_id,
                        ProductName = product.Name,
                        ProductSlug = product.Slug,
                        ImageUrl = product.Images.FirstOrDefault() ?? "",
                        Price = product.Price,
                        OriginalPrice = product.OriginalPrice,
                        IsOnSale = product.IsOnSale,
                        IsInStock = product.Stock > 0,
                        AddedAt = item.added_at
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error loading wishlist item: {ex.Message}");
                }
            }

            // Initialize selection dictionary from pool
            SelectedWishlistItems = SelectionDictPool.Get();
            foreach (var item in WishlistItems)
            {
                SelectedWishlistItems[item.Id] = false;
            }

            Console.WriteLine($"✅ Loaded {WishlistItems.Count} wishlist items");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error loading wishlist: {ex.Message}");
        }
    }

    private async Task LoadCart()
    {
        try
        {
            CartSummary = await CartService.GetCartSummaryAsync(CurrentUserId!);
            CartItems = CartSummary.Items;

            // Validate cart
            ValidationResult = await CartService.ValidateCartAsync(CurrentUserId!);

            if (ValidationResult != null && !ValidationResult.IsValid)
            {
                Console.WriteLine($"⚠️ Cart validation issues: {ValidationResult.Errors.Count}");
            }

            Console.WriteLine($"✅ Loaded cart with {CartItems.Count} items, total: {CartSummary.DisplayTotal}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error loading cart: {ex.Message}");
        }
    }

    private async Task SwitchTab(TabType tab)
    {
        if (ActiveTab == tab) return;

        ActiveTab = tab;
        
        // Reset pagination
        if (tab == TabType.Wishlist)
        {
            CurrentWishlistPage = 1;
        }
        else
        {
            CurrentCartPage = 1;
        }

        await LoadData();
    }

    // ==================== WISHLIST ACTIONS ====================

    private async Task RemoveFromWishlist(string productId)
    {
        try
        {
            var success = await WishlistService.RemoveFromWishlistAsync(CurrentUserId!, productId);
            if (success)
            {
                WishlistItems.RemoveAll(i => i.ProductId == productId);
                SelectedWishlistItems.Remove($"{CurrentUserId}_{productId}");
                Console.WriteLine($"✅ Removed from wishlist: {productId}");
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error removing from wishlist: {ex.Message}");
        }
    }

    private async Task MoveToCart(string productId)
    {
        try
        {
            if (!await PermissionService.CanAddToCartAsync())
            {
                return;
            }

            var success = await CartService.AddToCartAsync(CurrentUserId!, productId, 1);
            if (success)
            {
                Console.WriteLine($"✅ Added to cart: {productId}");
                await RemoveFromWishlist(productId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error moving to cart: {ex.Message}");
        }
    }

    private async Task MoveSelectedToCart()
    {
        try
        {
            var selectedIds = SelectedWishlistItems.Where(x => x.Value).Select(x => x.Key).ToList();
            if (!selectedIds.Any()) return;

            foreach (var compositeId in selectedIds)
            {
                var (userId, productId) = WishlistItemViewModel.ParseCompositeId(compositeId);
                await MoveToCart(productId);
            }

            SelectedWishlistItems.Clear();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error moving selected to cart: {ex.Message}");
        }
    }

    private void RemoveSelectedFromWishlist()
    {
        var selectedIds = SelectedWishlistItems.Where(x => x.Value).Select(x => x.Key).ToList();
        if (!selectedIds.Any()) return;

        ConfirmationTitle = "Remove Selected Items";
        ConfirmationMessage = $"Are you sure you want to remove {selectedIds.Count} item(s) from your wishlist?";
        ConfirmButtonText = "Remove";
        PendingAction = async () =>
        {
            foreach (var compositeId in selectedIds)
            {
                var (userId, productId) = WishlistItemViewModel.ParseCompositeId(compositeId);
                await RemoveFromWishlist(productId);
            }
            SelectedWishlistItems.Clear();
        };
        ConfirmationPopup?.Open();
    }

    private void ClearWishlist()
    {
        ConfirmationTitle = "Clear Wishlist";
        ConfirmationMessage = "Are you sure you want to remove all items from your wishlist?";
        ConfirmButtonText = "Clear All";
        PendingAction = async () =>
        {
            var success = await WishlistService.ClearWishlistAsync(CurrentUserId!);
            if (success)
            {
                WishlistItems.Clear();
                SelectedWishlistItems.Clear();
                Console.WriteLine("✅ Wishlist cleared");
                StateHasChanged();
            }
        };
        ConfirmationPopup?.Open();
    }

    private void ChangeWishlistPage(int page)
    {
        if (page < 1 || page > TotalWishlistPages) return;
        CurrentWishlistPage = page;
        StateHasChanged();
    }

    // ==================== CART ACTIONS ====================

    private async Task RemoveFromCart(string cartItemId)
    {
        try
        {
            var success = await CartService.RemoveFromCartByIdAsync(CurrentUserId!, cartItemId);
            if (success)
            {
                Console.WriteLine($"✅ Removed from cart: {cartItemId}");
                await LoadCart();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error removing from cart: {ex.Message}");
        }
    }

    private async Task SaveForLater(string productId)
    {
        try
        {
            if (!await PermissionService.CanAddToWishlistAsync())
            {
                return;
            }

            var success = await WishlistService.AddToWishlistAsync(CurrentUserId!, productId);
            if (success)
            {
                Console.WriteLine($"✅ Saved for later: {productId}");
                
                // Remove from cart
                var cartItem = CartItems.FirstOrDefault(i => i.ProductId == productId);
                if (cartItem != null)
                {
                    await RemoveFromCart(cartItem.Id);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error saving for later: {ex.Message}");
        }
    }

    private async Task IncreaseQuantity(string cartItemId)
    {
        try
        {
            var item = CartItems.FirstOrDefault(i => i.Id == cartItemId);
            if (item == null || !item.CanIncreaseQuantity) return;

            var success = await CartService.UpdateCartItemQuantityAsync(CurrentUserId!, cartItemId, item.Quantity + 1);
            if (success)
            {
                Console.WriteLine($"✅ Increased quantity: {cartItemId}");
                await LoadCart();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error increasing quantity: {ex.Message}");
        }
    }

    private async Task DecreaseQuantity(string cartItemId)
    {
        try
        {
            var item = CartItems.FirstOrDefault(i => i.Id == cartItemId);
            if (item == null || !item.CanDecreaseQuantity) return;

            var success = await CartService.UpdateCartItemQuantityAsync(CurrentUserId!, cartItemId, item.Quantity - 1);
            if (success)
            {
                Console.WriteLine($"✅ Decreased quantity: {cartItemId}");
                await LoadCart();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error decreasing quantity: {ex.Message}");
        }
    }

    private async Task UpdateQuantity(string cartItemId, string? quantityStr)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(quantityStr) || !int.TryParse(quantityStr, out int quantity))
            {
                return;
            }

            var item = CartItems.FirstOrDefault(i => i.Id == cartItemId);
            if (item == null) return;

            if (quantity < 1) quantity = 1;
            if (quantity > item.MaxQuantity) quantity = item.MaxQuantity;

            var success = await CartService.UpdateCartItemQuantityAsync(CurrentUserId!, cartItemId, quantity);
            if (success)
            {
                Console.WriteLine($"✅ Updated quantity: {cartItemId} to {quantity}");
                await LoadCart();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error updating quantity: {ex.Message}");
        }
    }

    private void ClearCart()
    {
        ConfirmationTitle = "Clear Cart";
        ConfirmationMessage = "Are you sure you want to remove all items from your cart?";
        ConfirmButtonText = "Clear All";
        PendingAction = async () =>
        {
            var success = await CartService.ClearCartAsync(CurrentUserId!);
            if (success)
            {
                CartItems.Clear();
                CartSummary = new CartSummaryViewModel();
                Console.WriteLine("✅ Cart cleared");
                StateHasChanged();
            }
        };
        ConfirmationPopup?.Open();
    }

    private void ChangeCartPage(int page)
    {
        if (page < 1 || page > TotalCartPages) return;
        CurrentCartPage = page;
        StateHasChanged();
    }

    private async Task ApplyPromoCode()
    {
        if (string.IsNullOrWhiteSpace(PromoCodeInput))
        {
            PromoMessage = "Please enter a promo code";
            PromoMessageType = "error";
            return;
        }

        IsApplyingPromo = true;
        StateHasChanged();

        try
        {
            // TODO: Implement actual promo code validation
            await Task.Delay(500);
            
            PromoMessage = "Promo code applied successfully!";
            PromoMessageType = "success";
            
            Console.WriteLine($"✅ Applied promo code: {PromoCodeInput}");
        }
        catch (Exception ex)
        {
            PromoMessage = "Invalid promo code";
            PromoMessageType = "error";
            Console.WriteLine($"❌ Error applying promo: {ex.Message}");
        }
        finally
        {
            IsApplyingPromo = false;
            StateHasChanged();
        }
    }

    // ==================== NAVIGATION ====================

    private void NavigateToProduct(string slug)
    {
        Navigation.NavigateTo($"/product/{slug}");
    }

    private void NavigateToShop()
    {
        Navigation.NavigateTo("/shop");
    }

    private async Task ProceedToCheckout()
    {
        if (!CanCheckout)
        {
            Console.WriteLine("❌ Cannot proceed to checkout");
            return;
        }

        if (!await PermissionService.CanCheckoutAsync())
        {
            return;
        }

        Navigation.NavigateTo("/checkout");
    }

    // ==================== CONFIRMATION ====================

    private async void ConfirmAction()
    {
        if (PendingAction != null)
        {
             PendingAction.Invoke();
            PendingAction = null;
        }
    }

    // ==================== CLEANUP ====================

    public void Dispose()
    {
        // Return selection dictionary to pool
        if (SelectedWishlistItems != null)
        {
            SelectionDictPool.Return(SelectedWishlistItems);
        }
    }

    // ==================== ENUMS ====================

    private enum TabType
    {
        Wishlist,
        Cart
    }
}
