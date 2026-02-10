using Microsoft.AspNetCore.Components;
using SubashaVentures.Domain.Product;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.Cart;
using SubashaVentures.Services.Wishlist;
using SubashaVentures.Services.VisualElements;
using SubashaVentures.Models.Firebase;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.Tracking;
using SubashaVentures.Components.Shared.Modals;
using System.Diagnostics;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Product;

public partial class ProductDetails : ComponentBase, IDisposable
{
    [Parameter] public string Slug { get; set; } = string.Empty;

    [Inject] private IProductService ProductService { get; set; } = default!;
    [Inject] private IReviewService ReviewService { get; set; } = default!;
    [Inject] private IPermissionService PermissionService { get; set; } = default!;
    [Inject] private ICartService CartService { get; set; } = default!;
    [Inject] private IWishlistService WishlistService { get; set; } = default!;
    [Inject] private IProductInteractionService InteractionService { get; set; } = default!;
    [Inject] private IVisualElementsService VisualElements { get; set; } = default!;
    [Inject] private ProductViewTracker ViewTracker { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<ProductDetails> Logger { get; set; } = default!;

    private ProductViewModel? Product;
    private List<ReviewViewModel> Reviews = new();
    private List<ProductViewModel> RelatedProducts = new();
    
    private bool IsLoading = true;
    private bool IsAuthenticated = false;
    private bool IsInCart = false;
    private bool IsInWishlist = false;
    private bool IsAddingToCart = false;
    private bool IsTogglingWishlist = false;
    private bool IsPurchasing = false;
    private bool ShowReviewForm = false;
    private bool ShowReviewConfirmation = false;
    private bool ImageLoading = true;
    private bool ThumbnailsLoading = true;
    
    private string? CurrentUserId;
    private string SelectedImage = string.Empty;
    private int SelectedQuantity = 1;
    
    // Variant selection
    private string? SelectedSize = null;
    private string? SelectedColor = null;
    private string? CurrentVariantKey = null;
    
    // SVG icons
    private string starSvg = string.Empty;
    private string flameSvg = string.Empty;
    private string warningSvg = string.Empty;
    private string checkMarkSvg = string.Empty;
    private string closeSvg = string.Empty;
    private string cartSvg = string.Empty;
    private string heartSvg = string.Empty;
    private string shippingSvg = string.Empty;
    private string weightSvg = string.Empty;
    private string buyNowSvg = string.Empty;
    private string editSvg = string.Empty;
    private string allProductsSvg = string.Empty;
    
    // Track time spent on page
    private Stopwatch? _pageViewStopwatch;
    private DateTime _pageLoadTime;
    
    // Reference to confirmation popup
    private ConfirmationPopup? ReviewConfirmationPopup;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            IsLoading = true;
            _pageLoadTime = DateTime.UtcNow;
            _pageViewStopwatch = Stopwatch.StartNew();

            await MID_HelperFunctions.DebugMessageAsync(
                $"🔄 Loading product details for slug: {Slug}",
                LogLevel.Info
            );

            // Load SVGs first
            await LoadSvgsAsync();

            IsAuthenticated = await PermissionService.IsAuthenticatedAsync();
            if (IsAuthenticated)
            {
                CurrentUserId = await PermissionService.GetCurrentUserIdAsync();
            }

            await LoadProductDetails();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Initializing product details");
            Logger.LogError(ex, "Failed to initialize product details page");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadSvgsAsync()
    {
        try
        {
            var tasks = new[]
            {
                VisualElements.GetCustomSvgAsync(SvgType.Star, width: 16, height: 16, fillColor: "currentColor"),
                VisualElements.GetCustomSvgAsync(SvgType.Flame, width: 16, height: 16, fillColor: "currentColor"),
                VisualElements.GetCustomSvgAsync(SvgType.Warning, width: 20, height: 20, fillColor: "currentColor"),
                VisualElements.GetCustomSvgAsync(SvgType.CheckMark, width: 18, height: 18, fillColor: "currentColor"),
                VisualElements.GetCustomSvgAsync(SvgType.Close, width: 18, height: 18, fillColor: "currentColor"),
                VisualElements.GetCustomSvgAsync(SvgType.Cart, width: 20, height: 20, fillColor: "currentColor"),
                VisualElements.GetCustomSvgAsync(SvgType.Heart, width: 20, height: 20, fillColor: "currentColor"),
                Task.Run(async () => VisualElements.GenerateSvg(
                    "<path d='M3 3h18v2H3V3zm0 6h18v2H3V9zm0 6h18v2H3v-2z'/><path d='M19 7l-5 5 5 5'/>",
                    width: 20, height: 20, viewBox: "0 0 24 24"
                )), // Shipping truck icon
                Task.Run(async () => VisualElements.GenerateSvg(
                    "<path d='M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm-1-13h2v6h-2zm0 8h2v2h-2z'/>",
                    width: 20, height: 20, viewBox: "0 0 24 24"
                )), // Weight scale icon
                Task.Run(async () => VisualElements.GenerateSvg(
                    "<path d='M13 3l-6 9h4v9l6-9h-4V3z'/>",
                    width: 20, height: 20, viewBox: "0 0 24 24"
                )), // Buy now lightning bolt
                Task.Run(async () => VisualElements.GenerateSvg(
                    "<path d='M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z'/>",
                    width: 18, height: 18, viewBox: "0 0 24 24"
                )), // Edit/write review pencil
                VisualElements.GetCustomSvgAsync(SvgType.AllProducts, width: 64, height: 64, fillColor: "var(--gray-400)")
            };

            var results = await Task.WhenAll(tasks);

            starSvg = results[0];
            flameSvg = results[1];
            warningSvg = results[2];
            checkMarkSvg = results[3];
            closeSvg = results[4];
            cartSvg = results[5];
            heartSvg = results[6];
            shippingSvg = results[7];
            weightSvg = results[8];
            buyNowSvg = results[9];
            editSvg = results[10];
            allProductsSvg = results[11];

            await MID_HelperFunctions.DebugMessageAsync(
                "✓ All product detail SVGs loaded successfully",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading product detail SVGs");
        }
    }

    protected override async Task OnParametersSetAsync()
{
    // Handle slug changes (e.g., from related products)
    if (!IsLoading && Product != null && !Product.Slug.Equals(Slug, StringComparison.OrdinalIgnoreCase))
    {
        await MID_HelperFunctions.DebugMessageAsync(
            $"🔄 Slug changed from {Product.Slug} to {Slug}, reloading...",
            LogLevel.Info
        );
        
        IsLoading = true;
        ImageLoading = true;  // Only show skeleton when loading new product
        ThumbnailsLoading = true;
        StateHasChanged();
        
        await LoadProductDetails();
        
        IsLoading = false;
        StateHasChanged();
    }
}

private async Task LoadProductDetails()
{
    try
    {
        // Get all products and find by slug
        var allProducts = await ProductService.GetAllProductsAsync();
        Product = allProducts.FirstOrDefault(p => 
            p.Slug.Equals(Slug, StringComparison.OrdinalIgnoreCase) && 
            p.IsActive && 
            !string.IsNullOrEmpty(p.Name)
        );

        if (Product == null)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"❌ Product not found for slug: {Slug}",
                LogLevel.Warning
            );
            return;
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"✅ Product loaded: {Product.Name} (ID: {Product.Id})",
            LogLevel.Info
        );

        // Set selected image
        SelectedImage = Product.Images?.FirstOrDefault() ?? string.Empty;
        
        // Reset image loading states after product is loaded
        ImageLoading = false;
        ThumbnailsLoading = false;
        
        // Initialize variant selection to first available
        if (Product.Sizes != null && Product.Sizes.Any())
        {
            SelectedSize = Product.Sizes.First();
        }
        
        if (Product.Colors != null && Product.Colors.Any())
        {
            SelectedColor = Product.Colors.First();
        }
        
        UpdateVariantKey();

        // Track product view
        if (!string.IsNullOrEmpty(CurrentUserId))
        {
            await InteractionService.TrackViewAsync(Product.Id, CurrentUserId);
        }

        // Track in localStorage for history page
        await ViewTracker.TrackProductViewAsync(Product);

        // Load user-specific data
        if (IsAuthenticated && !string.IsNullOrEmpty(CurrentUserId))
        {
            await CheckCartAndWishlistStatus();
        }

        // Load reviews
        await LoadReviews();

        // Load related products
        await LoadRelatedProducts();

        StateHasChanged();
    }
    catch (Exception ex)
    {
        await MID_HelperFunctions.LogExceptionAsync(ex, $"Loading product details: {Slug}");
        Logger.LogError(ex, "Failed to load product details");
    }
}

private void SelectImage(string image)
{
    // Don't show skeleton when switching between already-loaded images
    SelectedImage = image;
    StateHasChanged();
}
    
    private void HandleImageLoad()
{
    // Additional safety - ensure loading states are false when image loads
    ImageLoading = false;
    ThumbnailsLoading = false;
    StateHasChanged();
}

    private async Task CheckCartAndWishlistStatus()
    {
        if (Product == null || string.IsNullOrEmpty(CurrentUserId)) return;
        
        try
        {
            IsInCart = await CartService.IsInCartAsync(CurrentUserId, Product.Id.ToString());
            IsInWishlist = await WishlistService.IsInWishlistAsync(CurrentUserId, Product.Id.ToString());
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"📊 Cart status: {IsInCart}, Wishlist status: {IsInWishlist}",
                LogLevel.Debug
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Checking cart/wishlist status");
        }
    }

    private void UpdateVariantKey()
    {
        CurrentVariantKey = ProductModelExtensions.BuildVariantKey(SelectedSize, SelectedColor);
        
        // Update selected image if variant has specific image
        if (Product != null && !string.IsNullOrEmpty(CurrentVariantKey))
        {
            var variantImage = Product.GetVariantImage(CurrentVariantKey);
            if (!string.IsNullOrEmpty(variantImage))
            {
                SelectedImage = variantImage;
            }
        }
    }

    private void SelectSize(string size)
    {
        SelectedSize = size;
        UpdateVariantKey();
        StateHasChanged();
    }

    private void SelectColor(string color)
    {
        SelectedColor = color;
        UpdateVariantKey();
        StateHasChanged();
    }

    private string? GetColorHex(string color)
    {
        if (Product == null || string.IsNullOrEmpty(CurrentVariantKey)) return null;
        
        if (Product.Variants.TryGetValue(CurrentVariantKey, out var variant))
        {
            return variant.ColorHex;
        }
        
        return null;
    }

    private string GetVariantPrice()
    {
        if (Product == null) return "₦0";
        var price = Product.GetVariantPrice(CurrentVariantKey);
        return $"₦{price:N0}";
    }

    private int GetVariantStock()
    {
        if (Product == null) return 0;
        return Product.GetVariantStock(CurrentVariantKey);
    }

    private decimal GetVariantShippingCost()
    {
        if (Product == null) return 0;
        return Product.GetVariantShippingCost(CurrentVariantKey);
    }

    private bool GetVariantHasFreeShipping()
    {
        if (Product == null) return false;
        var productModel = Product.ToCloudModel();
        return productModel.VariantHasFreeShipping(CurrentVariantKey);
    }

    private decimal GetVariantWeight()
    {
        if (Product == null) return 0;
        var productModel = Product.ToCloudModel();
        return productModel.GetVariantWeight(CurrentVariantKey);
    }

    private string GetStockStatus()
    {
        var stock = GetVariantStock();
        return stock switch
        {
            0 => "Out of Stock",
            > 0 and <= 5 => "Low Stock",
            _ => "In Stock"
        };
    }

    private async Task LoadReviews()
    {
        try
        {
            if (Product == null) return;

            var reviewModels = await ReviewService.GetProductReviewsAsync(Product.Id.ToString());
            Reviews = ReviewViewModel.FromCloudModels(reviewModels);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading product reviews");
            Logger.LogError(ex, "Failed to load reviews");
            Reviews = new List<ReviewViewModel>();
        }
    }

    private async Task LoadRelatedProducts()
    {
        try
        {
            if (Product == null) return;

            var categoryProducts = await ProductService.GetProductsByCategoryAsync(Product.CategoryId);
            
            RelatedProducts = categoryProducts
                .Where(p => p.Id != Product.Id && p.IsActive && p.IsInStock)
                .OrderByDescending(p => p.IsFeatured)
                .ThenByDescending(p => p.Rating)
                .Take(4)
                .ToList();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Loaded {RelatedProducts.Count} related products",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading related products");
            Logger.LogError(ex, "Failed to load related products");
            RelatedProducts = new List<ProductViewModel>();
        }
    }

    private void SelectImage(string image)
    {
        ImageLoading = true;
        SelectedImage = image;
        StateHasChanged();
    }

    private void IncreaseQuantity()
    {
        if (SelectedQuantity < GetVariantStock())
        {
            SelectedQuantity++;
        }
    }

    private void DecreaseQuantity()
    {
        if (SelectedQuantity > 1)
        {
            SelectedQuantity--;
        }
    }

    private void ValidateQuantity()
    {
        var maxStock = GetVariantStock();
        if (SelectedQuantity < 1)
        {
            SelectedQuantity = 1;
        }
        else if (SelectedQuantity > maxStock)
        {
            SelectedQuantity = maxStock;
        }
    }

    private async Task HandleAddToCart()
    {
        if (Product == null || GetVariantStock() == 0 || IsAddingToCart) return;

        IsAddingToCart = true;
        StateHasChanged();

        try
        {
            if (!await PermissionService.EnsureAuthenticatedAsync($"product/{Product.Slug}"))
            {
                return;
            }

            if (string.IsNullOrEmpty(CurrentUserId))
            {
                CurrentUserId = await PermissionService.GetCurrentUserIdAsync();
            }

            if (string.IsNullOrEmpty(CurrentUserId))
            {
                PermissionService.ShowAuthRequiredMessage("add items to cart");
                return;
            }

            var success = await CartService.AddToCartAsync(
                CurrentUserId,
                Product.Id.ToString(),
                quantity: SelectedQuantity,
                size: SelectedSize,
                color: SelectedColor
            );

            if (success)
            {
                IsInCart = true;
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ Added {SelectedQuantity}x {Product.Name} (Size: {SelectedSize}, Color: {SelectedColor}) to cart",
                    LogLevel.Info
                );

                StateHasChanged();
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"❌ Failed to add {Product.Name} to cart",
                    LogLevel.Error
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Adding product to cart");
            Logger.LogError(ex, "Failed to add product to cart");
        }
        finally
        {
            IsAddingToCart = false;
            StateHasChanged();
        }
    }

    private async Task HandleBuyNow()
    {
        if (Product == null || GetVariantStock() == 0 || IsPurchasing) return;

        IsPurchasing = true;
        StateHasChanged();

        try
        {
            if (!await PermissionService.EnsureAuthenticatedAsync($"product/{Product.Slug}"))
            {
                return;
            }

            if (string.IsNullOrEmpty(CurrentUserId))
            {
                CurrentUserId = await PermissionService.GetCurrentUserIdAsync();
            }

            if (string.IsNullOrEmpty(CurrentUserId))
            {
                PermissionService.ShowAuthRequiredMessage("purchase items");
                return;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"⚡ Buy Now clicked: {Product.Name}, Qty: {SelectedQuantity}, Size: {SelectedSize}, Color: {SelectedColor}",
                LogLevel.Info
            );

            // Build query parameters for checkout
            var queryParams = new List<string>
            {
                $"productId={Product.Id}",
                $"quantity={SelectedQuantity}"
            };

            if (!string.IsNullOrEmpty(SelectedSize))
            {
                queryParams.Add($"size={Uri.EscapeDataString(SelectedSize)}");
            }

            if (!string.IsNullOrEmpty(SelectedColor))
            {
                queryParams.Add($"color={Uri.EscapeDataString(SelectedColor)}");
            }

            var queryString = string.Join("&", queryParams);
            
            // Navigate to checkout with product details
            var checkoutUrl = $"checkout/{Product.Slug}?{queryString}";
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Navigating to checkout: {checkoutUrl}",
                LogLevel.Info
            );

            Navigation.NavigateTo(checkoutUrl);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Buy now");
            Logger.LogError(ex, "Failed to process buy now");
        }
        finally
        {
            IsPurchasing = false;
            StateHasChanged();
        }
    }

    private async Task HandleWishlistToggle()
    {
        if (Product == null || IsTogglingWishlist) return;

        IsTogglingWishlist = true;
        StateHasChanged();

        try
        {
            if (!await PermissionService.EnsureAuthenticatedAsync($"product/{Product.Slug}"))
            {
                return;
            }

            if (string.IsNullOrEmpty(CurrentUserId))
            {
                CurrentUserId = await PermissionService.GetCurrentUserIdAsync();
            }

            if (string.IsNullOrEmpty(CurrentUserId))
            {
                PermissionService.ShowAuthRequiredMessage("add items to wishlist");
                return;
            }

            var success = await WishlistService.ToggleWishlistAsync(CurrentUserId, Product.Id.ToString());

            if (success)
            {
                IsInWishlist = !IsInWishlist;

                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ {(IsInWishlist ? "Added to" : "Removed from")} wishlist: {Product.Name}",
                    LogLevel.Info
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Toggling wishlist");
            Logger.LogError(ex, "Failed to toggle wishlist");
        }
        finally
        {
            IsTogglingWishlist = false;
            StateHasChanged();
        }
    }

    private void NavigateToCart()
    {
        Navigation.NavigateTo("/user/wishlist-cart");
    }

    private void NavigateToWishlist()
    {
        Navigation.NavigateTo("/user/wishlist-cart");
    }

    private void OpenReviewForm()
    {
        ShowReviewForm = true;
    }

    private void CloseReviewForm()
    {
        ShowReviewForm = false;
    }

    private async Task HandleReviewSubmitted()
    {
        ShowReviewForm = false;
        ShowReviewConfirmation = true;
        await LoadReviews();
        StateHasChanged();
    }

    private void CloseReviewConfirmation()
    {
        ShowReviewConfirmation = false;
        StateHasChanged();
    }

    private async Task HandleRelatedAddToCart(int productId)
    {
        await MID_HelperFunctions.DebugMessageAsync($"🛒 Add to cart from related: {productId}", LogLevel.Info);
    }

    private async Task HandleRelatedToggleFavorite(int productId)
    {
        await MID_HelperFunctions.DebugMessageAsync($"❤️ Toggle favorite from related: {productId}", LogLevel.Info);
    }

    private void NavigateToShop()
    {
        Navigation.NavigateTo("/shop");
    }

    private void NavigateToCategory(string categoryId)
    {
        Navigation.NavigateTo($"/shop?category={categoryId}");
    }

    public void Dispose()
    {
        if (_pageViewStopwatch != null && Product != null)
        {
            _pageViewStopwatch.Stop();
            var durationSeconds = (int)_pageViewStopwatch.Elapsed.TotalSeconds;

            _ = Task.Run(async () =>
            {
                try
                {
                    await ViewTracker.UpdateViewDurationAsync(
                        Product.Id.ToString(), 
                        durationSeconds
                    );

                    await MID_HelperFunctions.DebugMessageAsync(
                        $"📊 User spent {durationSeconds}s on product {Product.Id}",
                        LogLevel.Info
                    );
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to track page duration");
                }
            });
        }
    }
}
