using Microsoft.AspNetCore.Components;
using SubashaVentures.Domain.Product;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Shop;
using SubashaVentures.Services.Storage;
using SubashaVentures.Layout.Shop;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Domain.Shop;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Shop;

public partial class Shop : ComponentBase, IDisposable
{
    [Inject] private IProductService ProductService { get; set; } = null!;
    [Inject] private ShopStateService ShopState { get; set; } = null!;
    [Inject] private IBlazorAppLocalStorageService LocalStorage { get; set; } = null!;

    // FIXED: Same key as SidePanel
    private const string CATEGORY_FILTER_KEY = "shop_category_filter";
    
    private List<ProductViewModel> AllProducts { get; set; } = new();
    private List<ProductViewModel> FilteredProducts { get; set; } = new();
    private List<ProductViewModel> CurrentPageProducts { get; set; } = new();
    
    private bool IsLoading { get; set; } = true;
    private bool HasError { get; set; }
    private string ErrorMessage { get; set; } = "";
    
    // Search and Sort
    private string SearchQuery { get; set; } = "";
    private string SelectedSort { get; set; } = "default";
    
    // Active Filters (matches FilterState structure)
    private List<string> ActiveCategories = new();
    private List<string> ActiveBrands = new();
    private int ActiveMinRating = 0;
    private decimal ActiveMinPrice = 0;
    private decimal ActiveMaxPrice = 1000000;
    private bool ActiveOnSale = false;
    private bool ActiveFreeShipping = false;
    
    // Pagination
    private int CurrentPage { get; set; } = 1;
    private int ItemsPerPage { get; set; } = 12;
    private int TotalPages => (int)Math.Ceiling((double)FilteredProducts.Count / ItemsPerPage);

    // Mobile Filter State
    public bool ShowMobileFilters { get; set; }
    
    // Data loading flags
    private bool ProductsLoaded = false;

    protected override async Task OnInitializedAsync()
    {
        // Subscribe to shop state events
        ShopState.OnSearchChanged += HandleSearchChanged;
        ShopState.OnFiltersChanged += HandleFiltersChanged;
        
        await LoadProducts();
        
        // FIXED: Check for pending category filter AFTER products are loaded
        if (ProductsLoaded)
        {
            await CheckAndApplyPendingCategoryFilter();
        }
    }

    private async Task LoadProducts()
    {
        IsLoading = true;
        HasError = false;
        ProductsLoaded = false;
        StateHasChanged();

        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Loading all products for shop page",
                LogLevel.Info
            );

            AllProducts = await ProductService.GetAllProductsAsync();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {AllProducts.Count} products",
                LogLevel.Info
            );

            ProductsLoaded = true;
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

    /// <summary>
    /// Check localStorage for pending category filter from SidePanel navigation
    /// FIXED: Now expects List<string> to match FilterState structure
    /// </summary>
    private async Task CheckAndApplyPendingCategoryFilter()
    {
        try
        {
            // Check if there's a pending category filter
            var hasPendingFilter = await LocalStorage.ContainsKeyAsync(CATEGORY_FILTER_KEY);
            
            if (!hasPendingFilter)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No pending category filter found",
                    LogLevel.Debug
                );
                return;
            }

            // FIXED: Read as List<string> to match FilterState structure
            var categoryFilter = await LocalStorage.GetItemAsync<List<string>>(CATEGORY_FILTER_KEY);
            
            if (categoryFilter == null || !categoryFilter.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Pending category filter is empty",
                    LogLevel.Warning
                );
                await LocalStorage.RemoveItemAsync(CATEGORY_FILTER_KEY);
                return;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Found pending category filter: [{string.Join(", ", categoryFilter)}]",
                LogLevel.Info
            );

            // Verify at least one category exists in our products
            var validCategories = categoryFilter
                .Where(cat => AllProducts.Any(p => 
                    !string.IsNullOrEmpty(p.Category) && 
                    p.Category.Equals(cat, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (!validCategories.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"None of the filter categories exist in products, ignoring filter",
                    LogLevel.Warning
                );
                await LocalStorage.RemoveItemAsync(CATEGORY_FILTER_KEY);
                return;
            }

            // Apply the category filter (using FilterState structure)
            ActiveCategories.Clear();
            ActiveCategories.AddRange(validCategories);
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Applied category filter: [{string.Join(", ", ActiveCategories)}]",
                LogLevel.Info
            );

            // Remove the pending filter from storage
            await LocalStorage.RemoveItemAsync(CATEGORY_FILTER_KEY);
            
            // Apply filters and update UI
            CurrentPage = 1;
            ApplyFilters();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Checking pending category filter");
            
            // Clean up on error
            try
            {
                await LocalStorage.RemoveItemAsync(CATEGORY_FILTER_KEY);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    // FIXED: Changed to private async Task (event handler)
    private async Task HandleFiltersChanged(FilterState filters)
    {
        // Don't apply filters if products aren't loaded yet
        if (!ProductsLoaded)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Ignoring filter change - products not loaded yet",
                LogLevel.Warning
            );
            return;
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"Filters changed: {filters.Categories.Count} categories, {filters.Brands.Count} brands",
            LogLevel.Info
        );

        // FIXED: Direct assignment (both are List<string>)
        ActiveCategories = new List<string>(filters.Categories);
        ActiveBrands = new List<string>(filters.Brands);
        ActiveMinRating = filters.MinRating;
        ActiveMinPrice = filters.MinPrice;
        ActiveMaxPrice = filters.MaxPrice;
        ActiveOnSale = filters.OnSale;
        ActiveFreeShipping = filters.FreeShipping;
        
        CurrentPage = 1;
        ApplyFilters();
        CloseMobileFilters();
    }

    // FIXED: Changed to private async Task (event handler)
    private async Task HandleSearchChanged(string query)
    {
        // Don't apply search if products aren't loaded yet
        if (!ProductsLoaded)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Ignoring search change - products not loaded yet",
                LogLevel.Warning
            );
            return;
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"Search query: '{query}'",
            LogLevel.Info
        );

        SearchQuery = query ?? "";
        CurrentPage = 1;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        // Safety check - don't apply filters if products aren't loaded
        if (!ProductsLoaded || !AllProducts.Any())
        {
            FilteredProducts = new List<ProductViewModel>();
            CurrentPageProducts = new List<ProductViewModel>();
            return;
        }

        // Start with all active products
        FilteredProducts = AllProducts
            .Where(p => p.IsActive && !string.IsNullOrEmpty(p.Name))
            .ToList();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var query = SearchQuery.ToLower().Trim();
            FilteredProducts = FilteredProducts.Where(p =>
                (p.Name?.ToLower().Contains(query) ?? false) ||
                (p.Description?.ToLower().Contains(query) ?? false) ||
                (p.Brand?.ToLower().Contains(query) ?? false) ||
                (p.Category?.ToLower().Contains(query) ?? false)
            ).ToList();
        }

        // Apply category filter (matches FilterState structure)
        if (ActiveCategories.Any())
        {
            FilteredProducts = FilteredProducts
                .Where(p => !string.IsNullOrEmpty(p.Category) && 
                           ActiveCategories.Any(ac => ac.Equals(p.Category, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        // Apply brand filter
        if (ActiveBrands.Any())
        {
            FilteredProducts = FilteredProducts
                .Where(p => !string.IsNullOrEmpty(p.Brand) && 
                           ActiveBrands.Contains(p.Brand))
                .ToList();
        }

        // Apply rating filter
        if (ActiveMinRating > 0)
        {
            FilteredProducts = FilteredProducts
                .Where(p => p.Rating >= ActiveMinRating)
                .ToList();
        }

        // Apply price filter
        FilteredProducts = FilteredProducts
            .Where(p => p.Price >= ActiveMinPrice && p.Price <= ActiveMaxPrice)
            .ToList();

        // Apply sale filter
        if (ActiveOnSale)
        {
            FilteredProducts = FilteredProducts
                .Where(p => p.IsOnSale)
                .ToList();
        }

        // Apply free shipping filter
        if (ActiveFreeShipping)
        {
            FilteredProducts = FilteredProducts
                .Where(p => p.Price >= 50000)
                .ToList();
        }

        // Apply sorting
        ApplySorting();
        
        // Update pagination
        UpdateCurrentPageProducts();
        
        StateHasChanged();
    }

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
    }

    private async Task ApplySortAndUpdate()
    {
        await MID_HelperFunctions.DebugMessageAsync(
            $"Sort changed to: {SelectedSort}",
            LogLevel.Info
        );
        
        ApplySorting();
        UpdateCurrentPageProducts();
        StateHasChanged();
    }

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
    }

    private void NextPage() => GoToPage(CurrentPage + 1);
    private void PreviousPage() => GoToPage(CurrentPage - 1);

    private List<int> GetVisiblePages()
    {
        var pages = new List<int>();
        if (TotalPages <= 5)
        {
            for (int i = 1; i <= TotalPages; i++) pages.Add(i);
        }
        else if (CurrentPage <= 3)
        {
            for (int i = 1; i <= 5; i++) pages.Add(i);
        }
        else if (CurrentPage >= TotalPages - 2)
        {
            for (int i = TotalPages - 4; i <= TotalPages; i++) pages.Add(i);
        }
        else
        {
            for (int i = CurrentPage - 2; i <= CurrentPage + 2; i++) pages.Add(i);
        }
        return pages;
    }

    private string GetResultsRange()
    {
        if (!FilteredProducts.Any()) return "0";
        
        var start = (CurrentPage - 1) * ItemsPerPage + 1;
        var end = Math.Min(CurrentPage * ItemsPerPage, FilteredProducts.Count);
        return $"{start}-{end}";
    }

    private async Task HandleAddToCart(int productId)
    {
        await MID_HelperFunctions.DebugMessageAsync($"Add to cart: {productId}", LogLevel.Info);
    }

    private async Task HandleToggleFavorite(int productId)
    {
        await MID_HelperFunctions.DebugMessageAsync($"Toggle favorite: {productId}", LogLevel.Info);
    }

    private async Task ResetFilters()
    {
        SearchQuery = "";
        ActiveCategories.Clear();
        ActiveBrands.Clear();
        ActiveMinRating = 0;
        ActiveMinPrice = 0;
        ActiveMaxPrice = 1000000;
        ActiveOnSale = false;
        ActiveFreeShipping = false;
        SelectedSort = "default";
        CurrentPage = 1;
        
        // Clear any pending category filter
        try
        {
            await LocalStorage.RemoveItemAsync(CATEGORY_FILTER_KEY);
        }
        catch
        {
            // Ignore errors
        }
        
        ApplyFilters();
        StateHasChanged();
    }

    // Mobile filter controls
    public void OpenMobileFilters()
    {
        ShowMobileFilters = true;
        StateHasChanged();
    }

    public void CloseMobileFilters()
    {
        ShowMobileFilters = false;
        StateHasChanged();
    }

    public void Dispose()
    {
        // Unsubscribe from events to prevent memory leaks
        ShopState.OnSearchChanged -= HandleSearchChanged;
        ShopState.OnFiltersChanged -= HandleFiltersChanged;
        
        // Clean up any pending filters
        try
        {
            _ = LocalStorage.RemoveItemAsync(CATEGORY_FILTER_KEY);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
