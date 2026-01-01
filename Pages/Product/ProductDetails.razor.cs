using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SubashaVentures.Domain.Product;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.Cart;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Navigation;
using SubashaVentures.Services.Wishlist;
using SubashaVentures.Utilities.HelperScripts;
using System.Text.Json;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Product;

public partial class ProductDetails : ComponentBase, IDisposable
{
    #region Injected Services
    
    [Inject] private IProductService ProductService { get; set; } = null!;
    [Inject] private IPermissionService PermissionService { get; set; } = null!;
    [Inject] private ICartService CartService { get; set; } = null!;
    [Inject] private IWishlistService WishlistService { get; set; } = null!;
    [Inject] private IReviewService ReviewService { get; set; } = null!;
    [Inject] private INavigationService NavigationService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    
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
    
    private bool IsFavorite { get; set; }
    private bool IsInCart { get; set; }
    private bool IsAddingToCart { get; set; }
    private bool IsTogglingWishlist { get; set; }
    private string? currentUserId;
    
    private string StatusMessage { get; set; } = "";
    private string StatusMessageType { get; set; } = "";
    private System.Threading.Timer? statusMessageTimer;
    
    private bool ShowReviewForm { get; set; } = false;
    
    // View tracking
    private DateTime pageLoadTime;
    private const string VIEWED_PRODUCTS_KEY = "viewed_products_history";
    
    #endregion

    #region Lifecycle Methods

    protected override async Task OnInitializedAsync()
    {
        pageLoadTime = DateTime.UtcNow;
        
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
            pageLoadTime = DateTime.UtcNow;
            await LoadProduct();
        }
    }

    public void Dispose()
    {
        statusMessageTimer?.Dispose();
        _ = TrackProductViewDuration();
    }

    #endregion

    #region View Tracking

    private async Task TrackProductView()
    {
        if (Product == null) return;

        try
        {
            var historyJson = await JS.InvokeAsync<string>("localStorage.getItem", VIEWED_PRODUCTS_KEY);
            var history = new List<ViewedProductItem>();

            if (!string.IsNullOrEmpty(historyJson))
            {
                history = JsonSerializer.Deserialize<List<ViewedProductItem>>(historyJson) ?? new();
            }

            history.RemoveAll(h => h.ProductId == Product.Id.ToString());

            history.Insert(0, new ViewedProductItem
            {
                ProductId = Product.Id.ToString(),
                ProductName = Product.Name,
                ImageUrl = Product.Images?.FirstOrDefault() ?? "/diverse-products-still-life.png",
                Price = Product.Price,
                ViewedAt = DateTime.UtcNow,
                DurationSeconds = 0
            });

            if (history.Count > 50)
            {
                history = history.Take(50).ToList();
            }

            var json = JsonSerializer.Serialize(history);
            await JS.InvokeVoidAsync("localStorage.setItem", VIEWED_PRODUCTS_KEY, json);

            await MID_HelperFunctions.DebugMessageAsync(
                $"✅ Tracked view for product: {Product.Name}",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Tracking product view");
        }
    }

    private async Task TrackProductViewDuration()
    {
        if (Product == null) return;

        try
        {
            var durationSeconds = (int)(DateTime.UtcNow - pageLoadTime).TotalSeconds;
            
            var historyJson = await JS.InvokeAsync<string>("localStorage.getItem", VIEWED_PRODUCTS_KEY);
            
            if (string.IsNullOrEmpty(historyJson)) return;

            var history = JsonSerializer.Deserialize<List<ViewedProductItem>>(historyJson);
            if (history == null) return;

            var item = history.FirstOrDefault(h => h.ProductId == Product.Id.ToString());
            if (item != null)
            {
                item.DurationSeconds = durationSeconds;
                
                var json = JsonSerializer.Serialize(history);
                await JS.InvokeVoidAsync("localStorage.setItem", VIEWED_PRODUCTS_KEY, json);

                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ Updated view duration: {durationSeconds}s for {Product.Name}",
                    LogLevel.Info
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Tracking view duration");
        }
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

                InitializeDefaults();
                await CheckCartAndWishlistStatus();
                await TrackProductView();
                
                _ = LoadReviews();
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

                    IsFavorite = await WishlistService.IsInWishlistAsync(currentUserId, Product.Id.ToString());
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

            var allProducts = await ProductService.GetProductsByCategoryAsync(Product.CategoryId);
            
            RelatedProductsList = allProducts
                .Where(p => p.Id != Product.Id && p.IsActive && p.Stock > 0)
                .OrderByDescending(p => p.Rating)
                .ThenByDescending(p => p.SalesCount)
                .Take(6)
                .ToList();

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

        if (Product.Sizes.Any())
        {
            SelectedSize = Product.Sizes.First();
        }

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
    }

    private void SelectColor(string color)
    {
        SelectedColor = color;
        StateHasChanged();
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
                ShowStatusMessage($"✓ Added {Quantity} {Product.Name} to cart!", "success");
            }
            else
            {
                ShowStatusMessage("Failed to add to cart. Please try again.", "error");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Adding to cart");
            ShowStatusMessage($"Error: {ex.Message}", "error");
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

            var success = await WishlistService.ToggleWishlistAsync(currentUserId, Product.Id.ToString());

            if (success)
            {
                IsFavorite = !IsFavorite;
                ShowStatusMessage(IsFavorite ? "✓ Added to wishlist!" : "Removed from wishlist", "success");
            }
            else
            {
                ShowStatusMessage("Failed to update wishlist. Please try again.", "error");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Toggling favorite");
            ShowStatusMessage($"Error: {ex.Message}", "error");
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
        
        await HandleAddToCart();
        
        if (IsInCart)
        {
            NavigationManager.NavigateTo("/checkout");
        }
    }

    private async Task HandleShare()
    {
        if (Product == null) return;

        try
        {
            var shareData = new
            {
                title = Product.Name,
                text = Product.Description,
                url = NavigationManager.Uri
            };

            await JS.InvokeVoidAsync("navigator.share", shareData);
        }
        catch
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", NavigationManager.Uri);
            ShowStatusMessage("Link copied to clipboard!", "success");
        }
    }

    private async Task ScrollToTop()
    {
        try
        {
            await JS.InvokeVoidAsync("window.scrollTo", new { top = 0, behavior = "smooth" });
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Scrolling to top");
        }
    }

    #endregion

    #region Status Messages

    private void ShowStatusMessage(string message, string type)
    {
        StatusMessage = message;
        StatusMessageType = type;
        StateHasChanged();

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
        await MID_HelperFunctions.DebugMessageAsync($"Review image clicked: {imageUrl}", LogLevel.Info);
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

    public class ViewedProductItem
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public DateTime ViewedAt { get; set; }
        public int DurationSeconds { get; set; }
    }
}
