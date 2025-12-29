using Microsoft.AspNetCore.Components;
using SubashaVentures.Domain.Product;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Navigation;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Product;

public partial class ProductDetails : ComponentBase
{
    #region Injected Services
    
    [Inject] private IProductService ProductService { get; set; } = null!;
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
    private bool IsFavorite { get; set; }
    
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
        if (Product == null) return;

        await MID_HelperFunctions.DebugMessageAsync(
            $"Add to cart: Product ID {Product.Id}, Quantity: {Quantity}, Size: {SelectedSize}, Color: {SelectedColor}",
            LogLevel.Info
        );

        // TODO: Implement cart functionality
        // await CartService.AddToCartAsync(Product.Id, Quantity, SelectedSize, SelectedColor);
    }

    private async Task HandleBuyNow()
    {
        if (Product == null) return;

        await MID_HelperFunctions.DebugMessageAsync(
            $"Buy now: Product ID {Product.Id}, Quantity: {Quantity}",
            LogLevel.Info
        );

        // TODO: Implement buy now functionality
        // await CartService.AddToCartAsync(Product.Id, Quantity, SelectedSize, SelectedColor);
        // NavigationService.NavigateTo("/checkout");
    }

    private void HandleToggleFavorite()
    {
        IsFavorite = !IsFavorite;
        StateHasChanged();

        MID_HelperFunctions.DebugMessage(
            $"Toggle favorite: Product ID {Product?.Id}, IsFavorite: {IsFavorite}",
            LogLevel.Info
        );

        // TODO: Implement wishlist functionality
    }

    private async Task HandleShare()
    {
        await MID_HelperFunctions.DebugMessageAsync(
            $"Share product: {Product?.Name}",
            LogLevel.Info
        );

        // TODO: Implement share functionality (Web Share API via JS Interop)
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
