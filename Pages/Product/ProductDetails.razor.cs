using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SubashaVentures.Domain.Product;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.Cart;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Navigation;
using SubashaVentures.Services.Wishlist;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Product;

public partial class ProductDetails : ComponentBase
{
    #region Injected Services
    
    [Inject] private IProductService ProductService { get; set; } = null!;
    [Inject] private IPermissionService PermissionService { get; set; } = null!;
    [Inject] private ICartService CartService { get; set; } = null!;
    [Inject] private IWishlistService WishlistService { get; set; } = null!;
    [Inject] private IReviewService ReviewService { get; set; } = null!;
    [Inject] private INavigationService NavigationService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    
    #endregion

    #region Parameters
    
    [Parameter] public string Slug { get; set; } = "";
    
    #endregion

    #region State Properties
    
    private ProductViewModel? Product { get; set; }
    private List<ReviewViewModel> Reviews { get; set; } = new();
    private List<ProductViewModel> RelatedProductsList { get; set; } = new();
    
    private bool IsLoading { get; set; } = true;
    private bool IsLoadingReviews { get; set; } = false;
    private bool IsLoadingRelated { get; set; } = false;
    
    private int SelectedImageIndex { get; set; } = 0;
    private string? SelectedSize { get; set; }
    private string? SelectedColor { get; set; }
    private int Quantity { get; set; } = 1;
    
    // Cart and Wishlist states
    private bool IsFavorite { get; set; }
    private bool IsInCart { get; set; }
    private bool IsAddingToCart { get; set; }
    private bool IsTogglingWishlist { get; set; }
    private string? currentUserId;
    
    // Status messages
    private string StatusMessage { get; set; } = "";
    private string StatusMessageType { get; set; } = ""; // "success" or "error"
    private System.Threading.Timer? statusMessageTimer;
    
    private bool ShowReviewForm { get; set; } = false;
    
    #endregion

    #region Lifecycle Methods

    protected override async Task OnInitializedAsync()
    {
        await MID_HelperFunctions.DebugMessageAsync(
            $"ProductDetails page initializing for slug: {Slug}", 
            LogLevel.Info
        );

        await LoadProduct();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!string.IsNullOrEmpty(Slug))
        {
            await LoadProduct();
        }
    }

    public void Dispose()
    {
        statusMessageTimer?.Dispose();
    }

    #endregion

    #region Data Loading

    private async Task LoadProduct()
    {
        IsLoading = true;
        StateHasChanged();

        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Loading product with slug: {Slug}", 
                LogLevel.Info
            );

            // Get all products and find by slug
            var products = await ProductService.GetAllProductsAsync();
            Product = products.FirstOrDefault(p => 
                p.Slug.Equals(Slug, StringComparison.OrdinalIgnoreCase)
            );

            if (Product == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Product not found with slug: {Slug}", 
                    LogLevel.Warning
                );
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Product loaded: {Product.Name}", 
                    LogLevel.Info
                );

                // Initialize default selections
                InitializeDefaults();
                
                // Check cart and wishlist status if authenticated
                await CheckCartAndWishlistStatus();
                
                // Load reviews
                _ = LoadReviews();
                
                // Load related products
                _ = LoadRelatedProducts();
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading product");
            Product = null;
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private async Task CheckCartAndWishlistStatus()
    {
        if (Product == null) return;

        try
        {
            if (await PermissionService.IsAuthenticatedAsync())
            {
                currentUserId = await PermissionService.GetCurrentUserIdAsync();
                
                if (!string.IsNullOrEmpty(currentUserId))
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"Checking cart/wishlist status for user: {currentUserId}",
                        LogLevel.Info
                    );

                    // Check wishlist
                    IsFavorite = await WishlistService.IsInWishlistAsync(currentUserId, Product.Id.ToString());
                    
                    // Check cart
                    IsInCart = await CartService.IsInCartAsync(currentUserId, Product.Id.ToString());

                    await MID_HelperFunctions.DebugMessageAsync(
                        $"Status: InWishlist={IsFavorite}, InCart={IsInCart}",
                        LogLevel.Info
                    );
                }
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Checking cart/wishlist status");
        }
    }

    private async Task LoadReviews()
    {
        if (Product == null) return;

        IsLoadingReviews = true;
        StateHasChanged();

        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Loading reviews for product: {Product.Id}",
                LogLevel.Info
            );

            // Get Firebase ReviewModels and convert to ReviewViewModels
            var reviewModels = await ReviewService.GetProductReviewsAsync(Product.Id.ToString());
            Reviews = ReviewViewModel.FromCloudModels(reviewModels);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {Reviews.Count} reviews",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading reviews");
            Reviews = new List<ReviewViewModel>();
        }
        finally
        {
            IsLoadingReviews = false;
            StateHasChanged();
        }
    }

    private async Task LoadRelatedProducts()
    {
        if (Product == null) return;

        IsLoadingRelated = true;
        StateHasChanged();

        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Loading related products for category: {Product.Category}",
                LogLevel.Info
            );

            // Get products from same category, excluding current product
            var allProducts = await ProductService.GetProductsByCategoryAsync(Product.CategoryId);
            
            RelatedProductsList = allProducts
                .Where(p => p.Id != Product.Id && p.IsActive && p.Stock > 0)
                .OrderByDescending(p => p.Rating)
                .ThenByDescending(p => p.SalesCount)
                .Take(6)
                .ToList();

            // If not enough products from category, add products from same brand
            if (RelatedProductsList.Count < 4 && !string.IsNullOrEmpty(Product.Brand))
            {
                var brandProducts = await ProductService.GetAllProductsAsync();
                var additionalProducts = brandProducts
                    .Where(p => 
                        p.Brand.Equals(Product.Brand, StringComparison.OrdinalIgnoreCase) &&
                        p.Id != Product.Id &&
                        !RelatedProductsList.Any(rp => rp.Id == p.Id) &&
                        p.IsActive && 
                        p.Stock > 0)
                    .OrderByDescending(p => p.Rating)
                    .Take(4 - RelatedProductsList.Count)
                    .ToList();

                RelatedProductsList.AddRange(additionalProducts);
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {RelatedProductsList.Count} related products",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading related products");
            RelatedProductsList = new List<ProductViewModel>();
        }
        finally
        {
            IsLoadingRelated = false;
            StateHasChanged();
        }
    }

    private void InitializeDefaults()
    {
        if (Product == null) return;

        // Set default size
        if (Product.Sizes.Any())
        {
            SelectedSize = Product.Sizes.First();
        }

        // Set default color
        if (Product.Colors.Any())
        {
            SelectedColor = Product.Colors.First();
        }
    }

    #endregion

    #region Image Gallery

    private string GetCurrentImage()
    {
        if (Product?.Images == null || !Product.Images.Any())
        {
            return "/diverse-products-still-life.png";
        }

        return Product.Images[SelectedImageIndex];
    }

    private void SelectImage(int index)
    {
        if (Product?.Images == null || index < 0 || index >= Product.Images.Count)
            return;

        SelectedImageIndex = index;
        StateHasChanged();
    }

    #endregion

    #region Variant Selection

    private void SelectSize(string size)
    {
        SelectedSize = size;
        StateHasChanged();

        MID_HelperFunctions.DebugMessage($"Size selected: {size}", LogLevel.Info);
    }

    private void SelectColor(string color)
    {
        SelectedColor = color;
        StateHasChanged();

        MID_HelperFunctions.DebugMessage($"Color selected: {color}", LogLevel.Info);
    }

    #endregion

    #region Quantity Controls

    private void IncreaseQuantity()
    {
        if (Product == null || Quantity >= Product.Stock) return;

        Quantity++;
        StateHasChanged();
    }

    private void DecreaseQuantity()
    {
        if (Quantity <= 1) return;

        Quantity--;
        StateHasChanged();
    }

    #endregion

    #region Action Handlers

    private async Task HandleAddToCart()
    {
        if (Product == null || !Product.IsInStock || IsAddingToCart) return;

        IsAddingToCart = true;
        ClearStatusMessage();
        StateHasChanged();

        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Add to cart: Product ID {Product.Id}, Quantity: {Quantity}, Size: {SelectedSize}, Color: {SelectedColor}",
                LogLevel.Info
            );

            // Check authentication
            if (!await PermissionService.EnsureAuthenticatedAsync($"product/{Product.Slug}"))
            {
                return;
            }

            if (string.IsNullOrEmpty(currentUserId))
            {
                currentUserId = await PermissionService.GetCurrentUserIdAsync();
            }

            if (string.IsNullOrEmpty(currentUserId))
            {
                PermissionService.ShowAuthRequiredMessage("add items to cart");
                return;
            }

            // Add to cart with selected variants and quantity
            var success = await CartService.AddToCartAsync(
                currentUserId,
                Product.Id.ToString(),
                Quantity,
                SelectedSize,
                SelectedColor
            );

            if (success)
            {
                IsInCart = true;
                
                ShowStatusMessage(
                    $"✓ Added {Quantity} {Product.Name} to cart!", 
                    "success"
                );

                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Added {Product.Name} to cart (Qty: {Quantity}, Size: {SelectedSize}, Color: {SelectedColor})",
                    LogLevel.Info
                );
            }
            else
            {
                ShowStatusMessage(
                    "Failed to add to cart. Please try again.", 
                    "error"
                );

                await MID_HelperFunctions.DebugMessageAsync(
                    $"❌ Failed to add {Product.Name} to cart",
                    LogLevel.Error
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Adding to cart");
            ShowStatusMessage(
                $"Error: {ex.Message}", 
                "error"
            );
        }
        finally
        {
            IsAddingToCart = false;
            StateHasChanged();
        }
    }

    private async Task HandleToggleFavorite()
    {
        if (Product == null || IsTogglingWishlist) return;

        IsTogglingWishlist = true;
        ClearStatusMessage();
        StateHasChanged();

        try
        {
            // Check authentication
            if (!await PermissionService.EnsureAuthenticatedAsync($"product/{Product.Slug}"))
            {
                return;
            }

            if (string.IsNullOrEmpty(currentUserId))
            {
                currentUserId = await PermissionService.GetCurrentUserIdAsync();
            }

            if (string.IsNullOrEmpty(currentUserId))
            {
                PermissionService.ShowAuthRequiredMessage("add items to wishlist");
                return;
            }

            // Toggle wishlist
            var success = await WishlistService.ToggleWishlistAsync(currentUserId, Product.Id.ToString());

            if (success)
            {
                IsFavorite = !IsFavorite;
                
                ShowStatusMessage(
                    IsFavorite 
                        ? $"✓ Added to wishlist!" 
                        : "Removed from wishlist", 
                    "success"
                );

                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Toggled wishlist for {Product.Name}: {(IsFavorite ? "Added" : "Removed")}",
                    LogLevel.Info
                );
            }
            else
            {
                ShowStatusMessage(
                    "Failed to update wishlist. Please try again.", 
                    "error"
                );

                await MID_HelperFunctions.DebugMessageAsync(
                    $"❌ Failed to toggle wishlist for {Product.Name}",
                    LogLevel.Error
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Toggling favorite");
            ShowStatusMessage(
                $"Error: {ex.Message}", 
                "error"
            );
        }
        finally
        {
            IsTogglingWishlist = false;
            StateHasChanged();
        }
    }

    private async Task HandleBuyNow()
    {
        if (Product == null) return;

        await MID_HelperFunctions.DebugMessageAsync(
            $"Buy now: Product ID {Product.Id}, Quantity: {Quantity}",
            LogLevel.Info
        );

        // TODO: Implement buy now functionality
        // This should add to cart and navigate to checkout
        await HandleAddToCart();
        
        if (IsInCart)
        {
            NavigationManager.NavigateTo("/checkout");
        }
    }

    private async Task HandleShare()
    {
        await MID_HelperFunctions.DebugMessageAsync(
            $"Share product: {Product?.Name}",
            LogLevel.Info
        );

        // TODO: Implement share functionality (Web Share API via JS Interop)
        ShowStatusMessage("Share feature coming soon!", "success");
    }

    #endregion

    #region Status Messages

    private void ShowStatusMessage(string message, string type)
    {
        StatusMessage = message;
        StatusMessageType = type;
        StateHasChanged();

        // Auto-clear after 5 seconds
        statusMessageTimer?.Dispose();
        statusMessageTimer = new System.Threading.Timer(
            _ => ClearStatusMessage(), 
            null, 
            5000, 
            System.Threading.Timeout.Infinite
        );
    }

    private void ClearStatusMessage()
    {
        StatusMessage = "";
        StatusMessageType = "";
        StateHasChanged();
    }

    #endregion

    #region Review Handlers

    private void OpenReviewForm()
    {
        ShowReviewForm = true;
        StateHasChanged();
    }

    private void CloseReviewForm()
    {
        ShowReviewForm = false;
        StateHasChanged();
    }

    private async Task HandleReviewSubmitted()
    {
        ShowReviewForm = false;
        await LoadReviews();
        ShowStatusMessage("Thank you for your review!", "success");
        StateHasChanged();
    }

    private async Task HandleHelpfulClick(ReviewViewModel review)
    {
        await MID_HelperFunctions.DebugMessageAsync(
            $"Mark review helpful: {review.Id}",
            LogLevel.Info
        );

        try
        {
            var success = await ReviewService.MarkReviewHelpfulAsync(review.Id);
            if (success)
            {
                await LoadReviews();
                ShowStatusMessage("Thank you for your feedback!", "success");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Marking review helpful");
        }
    }

    private async Task HandleReviewImageClick(string imageUrl)
    {
        await MID_HelperFunctions.DebugMessageAsync(
            $"Review image clicked: {imageUrl}",
            LogLevel.Info
        );

        // TODO: Open image in lightbox/modal
    }

    #endregion

    #region Navigation

    private void NavigateBack()
    {
        NavigationManager.NavigateTo("shop");
    }

    private void NavigateToShop()
    {
        NavigationManager.NavigateTo("shop");
    }

    #endregion
}
