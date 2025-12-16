using Microsoft.AspNetCore.Components;
using SubashaVentures.Domain.Product;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Categories;
using SubashaVentures.Services.Brands;
using SubashaVentures.Layout.Shop;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.ObjectPooling;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Shop;

public partial class Shop : ComponentBase, IDisposable
{
    #region Injected Services
    
    [Inject] private IProductService ProductService { get; set; } = null!;
    [Inject] private ICategoryService CategoryService { get; set; } = null!;
    [Inject] private IBrandService BrandService { get; set; } = null!;
    
    #endregion

    #region State Properties
    
    private List<ProductViewModel> AllProducts { get; set; } = new();
    private List<ProductViewModel> FilteredProducts { get; set; } = new();
    private List<ProductViewModel> CurrentPageProducts { get; set; } = new();
    
    private bool IsLoading { get; set; } = true;
    private bool HasError { get; set; }
    private string ErrorMessage { get; set; } = "";
    
    #endregion

    #region Filter State
    
    private List<string> SelectedCategories { get; set; } = new();
    private List<string> SelectedBrands { get; set; } = new();
    private int MinRating { get; set; } = 0;
    private int[] PriceRange { get; set; } = new[] { 0, 500 };
    private bool OnSale { get; set; }
    private bool FreeShipping { get; set; }
    private string SearchQuery { get; set; } = "";
    private string SelectedSort { get; set; } = "default";
    
    #endregion

    #region Pagination State
    
    private int CurrentPage { get; set; } = 1;
    private int ItemsPerPage { get; set; } = 12;
    private int TotalPages => (int)Math.Ceiling((double)FilteredProducts.Count / ItemsPerPage);
    
    #endregion

    #region Object Pool
    
    private MID_ComponentObjectPool<List<ProductViewModel>>? _productListPool;
    
    #endregion

    #region Lifecycle Methods

    protected override async Task OnInitializedAsync()
    {
        await MID_HelperFunctions.DebugMessageAsync("Shop page initializing", LogLevel.Info);
        
        // Initialize object pool
        _productListPool = new MID_ComponentObjectPool<List<ProductViewModel>>(
            () => new List<ProductViewModel>(100),
            list => list.Clear(),
            maxPoolSize: 5
        );

        await LoadProducts();
    }

    #endregion

    #region Data Loading

    private async Task LoadProducts()
    {
        IsLoading = true;
        HasError = false;
        StateHasChanged();

        try
        {
            await MID_HelperFunctions.DebugMessageAsync("Loading all products", LogLevel.Info);

            // Load products from service
            AllProducts = await ProductService.GetAllProductsAsync();

            if (AllProducts == null || !AllProducts.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync("No products found", LogLevel.Warning);
                AllProducts = new List<ProductViewModel>();
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Loaded {AllProducts.Count} products successfully", 
                    LogLevel.Info
                );
            }

            // Apply filters and update display
            ApplyFilters();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = "Unable to load products. Please try again.";
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading products");
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    #endregion

    #region Filter Handling

    public async Task HandleFiltersChange(ShopFilterPanel.FilterState filters)
    {
        await MID_HelperFunctions.DebugMessageAsync("Applying filters", LogLevel.Info);

        SelectedCategories = filters.Categories;
        SelectedBrands = filters.Brands;
        MinRating = filters.MinRating;
        PriceRange = filters.PriceRange;
        OnSale = filters.OnSale;
        FreeShipping = filters.FreeShipping;

        ApplyFilters();
    }

    public async Task HandleSearchChange(string query)
    {
        await MID_HelperFunctions.DebugMessageAsync($"Search query: {query}", LogLevel.Info);
        SearchQuery = query;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        using var pooledList = _productListPool?.GetPooled();
        var filtered = pooledList?.Object ?? new List<ProductViewModel>();

        // Start with all products
        filtered.AddRange(AllProducts);

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            filtered = filtered.Where(p => 
                p.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                (p.Description?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Brand?.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
        }

        // Apply category filter
        if (SelectedCategories.Any())
        {
            filtered = filtered.Where(p => 
                SelectedCategories.Contains(p.Category)
            ).ToList();
        }

        // Apply brand filter
        if (SelectedBrands.Any())
        {
            filtered = filtered.Where(p => 
                SelectedBrands.Contains(p.Brand)
            ).ToList();
        }

        // Apply rating filter
        if (MinRating > 0)
        {
            filtered = filtered.Where(p => p.Rating >= MinRating).ToList();
        }

        // Apply price range filter
        filtered = filtered.Where(p => 
            p.Price >= PriceRange[0] && p.Price <= PriceRange[1]
        ).ToList();

        // Apply on sale filter
        if (OnSale)
        {
            filtered = filtered.Where(p => p.IsOnSale).ToList();
        }

        // Apply free shipping filter (assuming products with price > 50 qualify)
        if (FreeShipping)
        {
            filtered = filtered.Where(p => p.Price >= 50).ToList();
        }

        FilteredProducts = filtered.ToList();

        // Apply sorting
        ApplySorting();

        // Reset to page 1
        CurrentPage = 1;
        UpdateCurrentPageProducts();
    }

    private async Task ResetFilters()
    {
        await MID_HelperFunctions.DebugMessageAsync("Resetting filters", LogLevel.Info);

        SelectedCategories.Clear();
        SelectedBrands.Clear();
        MinRating = 0;
        PriceRange = new[] { 0, 500 };
        OnSale = false;
        FreeShipping = false;
        SearchQuery = "";
        SelectedSort = "default";

        ApplyFilters();
    }

    #endregion

    #region Sorting

    private void ApplySorting()
    {
        FilteredProducts = SelectedSort switch
        {
            "price-asc" => FilteredProducts.OrderBy(p => p.Price).ToList(),
            "price-desc" => FilteredProducts.OrderByDescending(p => p.Price).ToList(),
            "rating-desc" => FilteredProducts.OrderByDescending(p => p.Rating).ToList(),
            "name-asc" => FilteredProducts.OrderBy(p => p.Name).ToList(),
            "newest" => FilteredProducts.OrderByDescending(p => p.CreatedAt).ToList(),
            _ => FilteredProducts
        };

        UpdateCurrentPageProducts();
    }

    #endregion

    #region Pagination

    private void UpdateCurrentPageProducts()
    {
        var skip = (CurrentPage - 1) * ItemsPerPage;
        CurrentPageProducts = FilteredProducts
            .Skip(skip)
            .Take(ItemsPerPage)
            .ToList();

        StateHasChanged();
    }

    private void GoToPage(int page)
    {
        if (page < 1 || page > TotalPages) return;

        CurrentPage = page;
        UpdateCurrentPageProducts();
        ScrollToTop();
    }

    private void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            GoToPage(CurrentPage + 1);
        }
    }

    private void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            GoToPage(CurrentPage - 1);
        }
    }

    private List<int> GetVisiblePages()
    {
        var pages = new List<int>();
        var maxVisible = 7;

        if (TotalPages <= maxVisible)
        {
            // Show all pages
            for (int i = 1; i <= TotalPages; i++)
            {
                pages.Add(i);
            }
        }
        else if (CurrentPage <= 4)
        {
            // Show first 7 pages
            for (int i = 1; i <= 7; i++)
            {
                pages.Add(i);
            }
        }
        else if (CurrentPage >= TotalPages - 3)
        {
            // Show last 7 pages
            for (int i = TotalPages - 6; i <= TotalPages; i++)
            {
                pages.Add(i);
            }
        }
        else
        {
            // Show pages around current page
            for (int i = CurrentPage - 3; i <= CurrentPage + 3; i++)
            {
                pages.Add(i);
            }
        }

        return pages;
    }

    private string GetResultsRange()
    {
        var start = (CurrentPage - 1) * ItemsPerPage + 1;
        var end = Math.Min(CurrentPage * ItemsPerPage, FilteredProducts.Count);
        return $"{start}-{end}";
    }

    #endregion

    #region Event Handlers

    private async Task HandleAddToCart(int productId)
    {
        await MID_HelperFunctions.DebugMessageAsync($"Add to cart: Product ID {productId}", LogLevel.Info);
        // TODO: Implement cart functionality
    }

    private async Task HandleToggleFavorite(int productId)
    {
        await MID_HelperFunctions.DebugMessageAsync($"Toggle favorite: Product ID {productId}", LogLevel.Info);
        // TODO: Implement wishlist functionality
    }

    private void ToggleMobileFilters()
    {
        // This will be handled by ShopLayout
        // Access parent layout if needed
    }

    #endregion

    #region Helper Methods

    private void ScrollToTop()
    {
        // Scroll to top of page (can be implemented via JS interop if needed)
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        _productListPool?.Dispose();
    }

    #endregion
}
