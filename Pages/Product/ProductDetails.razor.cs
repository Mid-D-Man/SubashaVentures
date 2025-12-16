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
    [Inject] private INavigationService NavigationService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    
    #endregion

    #region Parameters
    
    [Parameter] public string Slug { get; set; } = "";
    
    #endregion

    #region State Properties
    
    private ProductViewModel? Product { get; set; }
    private bool IsLoading { get; set; } = true;
    
    private int SelectedImageIndex { get; set; } = 0;
    private string? SelectedSize { get; set; }
    private string? SelectedColor { get; set; }
    private int Quantity { get; set; } = 1;
    private bool IsFavorite { get; set; }
    
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

    #region Navigation

    private void NavigateBack()
    {
        NavigationManager.NavigateTo("/shop");
    }

    private void NavigateToShop()
    {
        NavigationManager.NavigateTo("/shop");
    }

    #endregion
}
