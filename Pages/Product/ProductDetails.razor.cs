using Microsoft.AspNetCore.Components;
using SubashaVentures.Domain.Product;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.Cart;
using SubashaVentures.Services.Wishlist;
using SubashaVentures.Models.Firebase;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.Tracking;
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
    private bool ShowReviewForm = false;
    
    private string? CurrentUserId;
    private string SelectedImage = string.Empty;
    private int SelectedQuantity = 1;
    
    // ✅ Track time spent on page
    private Stopwatch? _pageViewStopwatch;
    private DateTime _pageLoadTime;

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

            // ✅ Track product view
            if (!string.IsNullOrEmpty(CurrentUserId))
            {
                await InteractionService.TrackViewAsync(Product.Id, CurrentUserId);
                await MID_HelperFunctions.DebugMessageAsync(
                    $"📊 Tracked view for product {Product.Id}",
                    LogLevel.Info
                );
            }

            // ✅ Track in localStorage for history page
            await ViewTracker.TrackProductViewAsync(Product);

            // Load user-specific data
            if (IsAuthenticated && !string.IsNullOrEmpty(CurrentUserId))
            {
                IsInCart = await CartService.IsInCartAsync(CurrentUserId, Product.Id.ToString());
                IsInWishlist = await WishlistService.IsInWishlistAsync(CurrentUserId, Product.Id.ToString());
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

    private async Task LoadReviews()
    {
        try
        {
            if (Product == null) return;

            var reviewModels = await ReviewService.GetProductReviewsAsync(Product.Id.ToString());
        
            // ✅ Convert using the existing method
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

            // Get products from same category, excluding current product
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
        SelectedImage = image;
        StateHasChanged();
    }

    private void IncreaseQuantity()
    {
        if (Product != null && SelectedQuantity < Product.Stock)
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

    private async Task HandleAddToCart()
    {
        if (Product == null || !Product.IsInStock || IsAddingToCart) return;

        IsAddingToCart = true;
        StateHasChanged();

        try
        {
            // Check authentication
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

            // ✅ Add to cart with user-specified quantity
            var success = await CartService.AddToCartAsync(
                CurrentUserId,
                Product.Id.ToString(),
                quantity: SelectedQuantity
            );

            if (success)
            {
                IsInCart = true;
                
                // ✅ Track add to cart interaction
                await InteractionService.TrackAddToCartAsync(Product.Id, CurrentUserId);

                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ Added {SelectedQuantity}x {Product.Name} to cart",
                    LogLevel.Info
                );

                // Reset quantity to 1 after adding
                SelectedQuantity = 1;
                StateHasChanged();

                // Show success message for 2 seconds
                await Task.Delay(2000);
                IsInCart = false;
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

    private async Task HandleWishlistToggle()
    {
        if (Product == null || IsTogglingWishlist) return;

        IsTogglingWishlist = true;
        StateHasChanged();

        try
        {
            // Check authentication
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

            // Toggle wishlist
            var success = await WishlistService.ToggleWishlistAsync(CurrentUserId, Product.Id.ToString());

            if (success)
            {
                IsInWishlist = !IsInWishlist;
                
                // ✅ Track wishlist interaction (only when adding)
                if (IsInWishlist)
                {
                    await InteractionService.TrackWishlistAsync(Product.Id, CurrentUserId);
                }

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
        await LoadReviews();
        StateHasChanged();
    }

    private void NavigateToShop()
    {
        Navigation.NavigateTo("shop");
    }

    private void NavigateToCategory(string categoryId)
    {
        Navigation.NavigateTo($"shop?category={categoryId}");
    }

   
    public void Dispose()
    {
        //  Track total time spent on page when user leaves
        if (_pageViewStopwatch != null && Product != null)
        {
            _pageViewStopwatch.Stop();
            var durationSeconds = (int)_pageViewStopwatch.Elapsed.TotalSeconds;

            // Save duration to localStorage for history page
            _ = Task.Run(async () =>
            {
                try
                {
                    await ViewTracker.UpdateViewDurationAsync(
                        Product.Id.ToString(), 
                        durationSeconds
                    );

                    await MID_HelperFunctions.DebugMessageAsync(
                        $"📊 User spent {durationSeconds}s ({_pageViewStopwatch.Elapsed:mm\\:ss}) on product {Product.Id}",
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