// Pages/User/WishlistCart.razor.cs - FIXED VERSION
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Domain.Cart;
using SubashaVentures.Domain.User;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.Cart;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Wishlist;
using SubashaVentures.Services.VisualElements;
using SubashaVentures.Utilities.ObjectPooling;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Models.Supabase;
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
    [Inject] private IVisualElementsService VisualElements { get; set; } = default!;

    private static readonly MID_ComponentObjectPool<Dictionary<string, bool>> SelectionDictPool =
        new(() => new Dictionary<string, bool>(), dict => dict.Clear(), maxPoolSize: 50);

    private TabType ActiveTab = TabType.Wishlist;
    private bool IsLoading = true;
    private string? CurrentUserId;

    private List<WishlistItemViewModel> WishlistItems = new();
    private Dictionary<string, bool> SelectedWishlistItems = new();
    private int CurrentWishlistPage = 1;
    private const int WishlistItemsPerPage = 12;

    private List<CartItemViewModel> CartItems = new();
    private CartSummaryViewModel CartSummary = new();
    private int CurrentCartPage = 1;
    private const int CartItemsPerPage = 10;
    private CartValidationResult? ValidationResult;

    private ConfirmationPopup? ConfirmationPopup;
    private string ConfirmationTitle = "";
    private string ConfirmationMessage = "";
    private string ConfirmButtonText = "Confirm";
    private Action? PendingAction;

    private string wishlistSvg = string.Empty;
    private string cartSvg = string.Empty;
    private string addToCartSvg = string.Empty;
    private string removeSvg = string.Empty;
    private string checkmarkSvg = string.Empty;
    private string arrowLeftSvg = string.Empty;
    private string arrowRightSvg = string.Empty;

    private int WishlistCount => WishlistItems.Count;
    private int CartCount => CartItems.Sum(i => i.Quantity);
    private int TotalWishlistItems => WishlistItems.Count;
    private int TotalWishlistPages => (int)Math.Ceiling(TotalWishlistItems / (double)WishlistItemsPerPage);
    private int TotalCartPages => (int)Math.Ceiling(CartItems.Count / (double)CartItemsPerPage);
    private bool CanCheckout => CartSummary.CanCheckout && ValidationResult?.IsValid != false;

    private int SelectedWishlistItemsCount => SelectedWishlistItems.Count(x => x.Value);

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
        await LoadSvgsAsync();
        await GetCurrentUserId();
        await LoadData();
    }

    private async Task LoadSvgsAsync()
    {
        try
        {
            wishlistSvg = await VisualElements.GetCustomSvgAsync(SvgType.Wishlist, width: 24, height: 24);
            cartSvg = await VisualElements.GetCustomSvgAsync(SvgType.Cart, width: 24, height: 24);
            checkmarkSvg = await VisualElements.GetCustomSvgAsync(SvgType.CheckMark, width: 20, height: 20);
            
            addToCartSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' fill='none' stroke-width='2' stroke-linecap='round' stroke-linejoin='round' d='M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2 5m12-5l2 5m-2-5v5m-5-5v5m-5-5v5'/>",
                24, 24, "0 0 24 24"
            );
            
            removeSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' fill='none' stroke-width='2' stroke-linecap='round' stroke-linejoin='round' d='M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16'/>",
                24, 24, "0 0 24 24"
            );
            
            arrowLeftSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' fill='none' stroke-width='2' stroke-linecap='round' stroke-linejoin='round' d='M15 19l-7-7 7-7'/>",
                24, 24, "0 0 24 24"
            );
            
            arrowRightSvg = VisualElements.GenerateSvg(
                "<path stroke='currentColor' fill='none' stroke-width='2' stroke-linecap='round' stroke-linejoin='round' d='M9 5l7 7-7 7'/>",
                24, 24, "0 0 24 24"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading SVGs: {ex.Message}");
        }
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
                Console.WriteLine("User not authenticated");
                Navigation.NavigateTo("signin?returnUrl=user/wishlist-cart");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting user ID: {ex.Message}");
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
            Console.WriteLine($"Error loading data: {ex.Message}");
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
                Console.WriteLine("Wishlist is empty");
                if (SelectedWishlistItems != null)
                {
                    SelectionDictPool.Return(SelectedWishlistItems);
                }
                SelectedWishlistItems = SelectionDictPool.Get();
                return;
            }

            var wishlist = wishlists[0];
            Console.WriteLine($"Loading {wishlist.Items.Count} wishlist items");

            foreach (var item in wishlist.Items)
            {
                try
                {
                    var productIdInt = int.Parse(item.product_id);
                    var product = await ProductService.GetProductByIdAsync(productIdInt);

                    if (product == null)
                    {
                        Console.WriteLine($"Product not found: {item.product_id}");
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
                    Console.WriteLine($"Error loading wishlist item: {ex.Message}");
                }
            }

            if (SelectedWishlistItems != null)
            {
                SelectionDictPool.Return(SelectedWishlistItems);
            }
            SelectedWishlistItems = SelectionDictPool.Get();
            foreach (var item in WishlistItems)
            {
                SelectedWishlistItems[item.Id] = false;
            }

            Console.WriteLine($"Loaded {WishlistItems.Count} wishlist items");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading wishlist: {ex.Message}");
        }
    }

    private async Task LoadCart()
    {
        try
        {
            CartSummary = await CartService.GetCartSummaryAsync(CurrentUserId!);
            CartItems = CartSummary.Items;

            ValidationResult = await CartService.ValidateCartAsync(CurrentUserId!);

            if (ValidationResult != null && !ValidationResult.IsValid)
            {
                Console.WriteLine($"Cart validation issues: {ValidationResult.Errors.Count}");
            }

            Console.WriteLine($"Loaded cart with {CartItems.Count} items, total: {CartSummary.DisplayTotal}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading cart: {ex.Message}");
        }
    }

    private async Task SwitchTab(TabType tab)
    {
        if (ActiveTab == tab) return;

        ActiveTab = tab;
        
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

    private async Task RemoveFromWishlist(string productId)
    {
        try
        {
            var success = await WishlistService.RemoveFromWishlistAsync(CurrentUserId!, productId);
            if (success)
            {
                WishlistItems.RemoveAll(i => i.ProductId == productId);
                SelectedWishlistItems.Remove($"{CurrentUserId}_{productId}");
                Console.WriteLine($"Removed from wishlist: {productId}");
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing from wishlist: {ex.Message}");
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

            var productIdInt = int.Parse(productId);
            var product = await ProductService.GetProductByIdAsync(productIdInt);

            if (product == null)
            {
                Console.WriteLine($"Product not found: {productId}");
                return;
            }

            if (!product.IsInStock)
            {
                Console.WriteLine($"Product out of stock: {productId}");
                return;
            }

            var productModel = product.ToCloudModel();
            
            if (productModel.Variants.Any())
            {
                var firstVariant = productModel.Variants.First();
                var size = firstVariant.Value.Size;
                var color = firstVariant.Value.Color;
                
                Console.WriteLine($"Product has variants, using first: Size={size}, Color={color}");
                
                var success = await CartService.AddToCartAsync(CurrentUserId!, productId, 1, size, color);
                if (success)
                {
                    Console.WriteLine($"Added to cart with variants: {productId}");
                    await RemoveFromWishlist(productId);
                }
            }
            else
            {
                var success = await CartService.AddToCartAsync(CurrentUserId!, productId, 1);
                if (success)
                {
                    Console.WriteLine($"Added to cart: {productId}");
                    await RemoveFromWishlist(productId);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error moving to cart: {ex.Message}");
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
                var parts = compositeId.Split('_');
                if (parts.Length >= 2)
                {
                    var productId = parts[1];
                    await MoveToCart(productId);
                }
            }

            foreach (var key in SelectedWishlistItems.Keys.ToList())
            {
                SelectedWishlistItems[key] = false;
            }
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error moving selected to cart: {ex.Message}");
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
                var parts = compositeId.Split('_');
                if (parts.Length >= 2)
                {
                    var productId = parts[1];
                    await RemoveFromWishlist(productId);
                }
            }
            foreach (var key in SelectedWishlistItems.Keys.ToList())
            {
                SelectedWishlistItems[key] = false;
            }
            ConfirmationPopup?.Close();
            StateHasChanged();
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
                foreach (var key in SelectedWishlistItems.Keys.ToList())
                {
                    SelectedWishlistItems[key] = false;
                }
                Console.WriteLine("Wishlist cleared");
                ConfirmationPopup?.Close();
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

    private async Task RemoveFromCart(string cartItemId)
    {
        try
        {
            var success = await CartService.RemoveFromCartByIdAsync(CurrentUserId!, cartItemId);
            if (success)
            {
                Console.WriteLine($"Removed from cart: {cartItemId}");
                await LoadCart();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing from cart: {ex.Message}");
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
                Console.WriteLine($"Saved for later: {productId}");
                
                var cartItem = CartItems.FirstOrDefault(i => i.ProductId == productId);
                if (cartItem != null)
                {
                    await RemoveFromCart(cartItem.Id);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving for later: {ex.Message}");
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
                Console.WriteLine($"Increased quantity: {cartItemId}");
                await LoadCart();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error increasing quantity: {ex.Message}");
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
                Console.WriteLine($"Decreased quantity: {cartItemId}");
                await LoadCart();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error decreasing quantity: {ex.Message}");
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
                Console.WriteLine($"Updated quantity: {cartItemId} to {quantity}");
                await LoadCart();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating quantity: {ex.Message}");
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
                Console.WriteLine("Cart cleared");
                ConfirmationPopup?.Close();
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

    private void NavigateToProduct(string slug)
    {
        Navigation.NavigateTo($"product/{slug}");
    }

    private void NavigateToShop()
    {
        Navigation.NavigateTo("shop");
    }

    private async Task ProceedToCheckout()
    {
        if (!CanCheckout)
        {
            Console.WriteLine("Cannot proceed to checkout");
            return;
        }

        if (!await PermissionService.CanCheckoutAsync())
        {
            return;
        }

        if (CartItems.Any())
        {
            var firstItem = CartItems.First();
            Navigation.NavigateTo($"checkout/{firstItem.Slug}");
        }
    }

    private async void ConfirmAction()
    {
        if (PendingAction != null)
        {
            await PendingAction.Invoke();
            PendingAction = null;
        }
    }

    public void Dispose()
    {
        if (SelectedWishlistItems != null)
        {
            SelectionDictPool.Return(SelectedWishlistItems);
        }
    }

    private enum TabType
    {
        Wishlist,
        Cart
    }
}
