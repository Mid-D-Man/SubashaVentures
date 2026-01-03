using Microsoft.AspNetCore.Components;
using SubashaVentures.Domain.Product;
using SubashaVentures.Domain.Shop;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Shop;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Shop;

public partial class Shop : ComponentBase, IDisposable
{
    [Inject] private IProductService ProductService { get; set; } = null!;
    [Inject] private ShopStateService ShopState { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    
    // Data
    private List<ProductViewModel> AllProducts { get; set; } = new();
    private List<ProductViewModel> FilteredProducts { get; set; } = new();
    private List<ProductViewModel> CurrentPageProducts { get; set; } = new();
    
    // State
    private bool IsLoading { get; set; } = true;
    private bool HasError { get; set; }
    private string ErrorMessage { get; set; } = "";
    private bool IsInitialized { get; set; } = false;
    
    // Current Filters (IN-MEMORY ONLY)
    private FilterState CurrentFilters { get; set; } = FilterState.CreateDefault();
    private string SelectedSort { get; set; } = "default";
    
    // Pagination
    private int CurrentPage { get; set; } = 1;
    private int ItemsPerPage { get; set; } = 12;
    private int TotalPages => (int)Math.Ceiling((double)FilteredProducts.Count / ItemsPerPage);

    // Mobile Filter State
    public bool ShowMobileFilters { get; set; }
    
    // Helper Properties
    private bool HasActiveFilters => CurrentFilters != null && !CurrentFilters.IsEmpty;
    private int ActiveFilterCount
    {
        get
        {
            if (CurrentFilters == null) return 0;
            int count = 0;
            count += CurrentFilters.Categories.Count;
            count += CurrentFilters.Brands.Count;
            if (CurrentFilters.MinRating > 0) count++;
            if (CurrentFilters.MinPrice > 0 || CurrentFilters.MaxPrice < 1000000) count++;
            if (CurrentFilters.OnSale) count++;
            if (CurrentFilters.FreeShipping) count++;
            if (!string.IsNullOrEmpty(CurrentFilters.SearchQuery)) count++;
            return count;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await MID_HelperFunctions.DebugMessageAsync(
            "🚀 Shop page initializing",
            LogLevel.Info
        );
        
        // Subscribe to state service events
        ShopState.OnSearchChanged += HandleSearchChanged;
        ShopState.OnFiltersChanged += HandleFiltersChangedFromState;
        
        // STEP 1: Load filters from ShopState (might have category from navigation)
        CurrentFilters = await ShopState.GetCurrentFiltersAsync();
        SelectedSort = CurrentFilters.SortBy;
        
        await MID_HelperFunctions.DebugMessageAsync(
            $"📋 Loaded filters: Categories=[{string.Join(", ", CurrentFilters.Categories)}], Search='{CurrentFilters.SearchQuery}'",
            LogLevel.Info
        );
        
        // STEP 2: Load products
        await LoadProducts();
        
        // STEP 3: Apply filters
        await ApplyFilters();
        
        IsInitialized = true;
        
        await MID_HelperFunctions.DebugMessageAsync(
            "✓ Shop page initialized successfully",
            LogLevel.Info
        );
    }

    private async Task LoadProducts()
    {
        IsLoading = true;
        HasError = false;
        StateHasChanged();

        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "📦 Loading all products",
                LogLevel.Info
            );

            AllProducts = await ProductService.GetAllProductsAsync();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {AllProducts.Count} products",
                LogLevel.Info
            );
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

    private async Task HandleFiltersChanged(FilterState filters)
    {
        if (!IsInitialized || !AllProducts.Any())
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "⚠ Ignoring filter change - not initialized or no products",
                LogLevel.Warning
            );
            return;
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"🔧 Filters changed: {filters.Categories.Count} categories, {filters.Brands.Count} brands",
            LogLevel.Info
        );

        CurrentFilters = filters.Clone();
        SelectedSort = CurrentFilters.SortBy;
        
        // Update ShopState
        await ShopState.UpdateFiltersAsync(CurrentFilters);
        
        CurrentPage = 1;
        await ApplyFilters();
        CloseMobileFilters();
    }

    private async Task HandleFiltersChangedFromState(FilterState filters)
    {
        await MID_HelperFunctions.DebugMessageAsync(
            $"🔄 Filters changed from state: Categories=[{string.Join(", ", filters.Categories)}]",
            LogLevel.Info
        );
        
        CurrentFilters = filters.Clone();
        SelectedSort = CurrentFilters.SortBy;
        
        if (IsInitialized && AllProducts.Any())
        {
            CurrentPage = 1;
            await ApplyFilters();
        }
    }

    private async Task HandleSearchChanged(string query)
    {
        if (!IsInitialized || !AllProducts.Any())
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "⚠ Ignoring search change - not initialized or no products",
                LogLevel.Warning
            );
            return;
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"🔍 Search query changed: '{query}'",
            LogLevel.Info
        );

        CurrentFilters.SearchQuery = query ?? "";
        
        CurrentPage = 1;
        await ApplyFilters();
    }

    private async Task HandleSortChanged()
    {
        if (!IsInitialized || !AllProducts.Any())
        {
            return;
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"📊 Sort changed to: {SelectedSort}",
            LogLevel.Info
        );
        
        CurrentFilters.SortBy = SelectedSort;
        await ShopState.UpdateFiltersAsync(CurrentFilters);
        
        ApplySorting();
        UpdateCurrentPageProducts();
        StateHasChanged();
    }

    private async Task ApplyFilters()
    {
        if (!AllProducts.Any())
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "⚠ Cannot apply filters - no products loaded",
                LogLevel.Debug
            );
            FilteredProducts = new List<ProductViewModel>();
            CurrentPageProducts = new List<ProductViewModel>();
            return;
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"🎯 Applying filters - Categories: [{string.Join(", ", CurrentFilters.Categories)}], Search: '{CurrentFilters.SearchQuery}'",
            LogLevel.Info
        );

        // Start with all active products
        FilteredProducts = AllProducts
            .Where(p => p.IsActive && !string.IsNullOrEmpty(p.Name))
            .ToList();

        await MID_HelperFunctions.DebugMessageAsync(
            $"After active filter: {FilteredProducts.Count} products",
            LogLevel.Debug
        );

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(CurrentFilters.SearchQuery))
        {
            var query = CurrentFilters.SearchQuery.ToLower().Trim();
            FilteredProducts = FilteredProducts.Where(p =>
                (p.Name?.ToLower().Contains(query) ?? false) ||
                (p.Description?.ToLower().Contains(query) ?? false) ||
                (p.Brand?.ToLower().Contains(query) ?? false) ||
                (p.Category?.ToLower().Contains(query) ?? false)
            ).ToList();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"After search filter: {FilteredProducts.Count} products",
                LogLevel.Debug
            );
        }

        // Apply category filter
        if (CurrentFilters.Categories.Any())
        {
            FilteredProducts = FilteredProducts
                .Where(p => !string.IsNullOrEmpty(p.Category) && 
                           CurrentFilters.Categories.Any(ac => ac.Equals(p.Category, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"After category filter: {FilteredProducts.Count} products",
                LogLevel.Debug
            );
        }

        // Apply brand filter
        if (CurrentFilters.Brands.Any())
        {
            FilteredProducts = FilteredProducts
                .Where(p => !string.IsNullOrEmpty(p.Brand) && 
                           CurrentFilters.Brands.Contains(p.Brand))
                .ToList();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"After brand filter: {FilteredProducts.Count} products",
                LogLevel.Debug
            );
        }

        // Apply rating filter
        if (CurrentFilters.MinRating > 0)
        {
            FilteredProducts = FilteredProducts
                .Where(p => p.Rating >= CurrentFilters.MinRating)
                .ToList();
        }

        // Apply price filter
        FilteredProducts = FilteredProducts
            .Where(p => p.Price >= CurrentFilters.MinPrice && p.Price <= CurrentFilters.MaxPrice)
            .ToList();

        // Apply sale filter
        if (CurrentFilters.OnSale)
        {
            FilteredProducts = FilteredProducts
                .Where(p => p.IsOnSale)
                .ToList();
        }

        // Apply free shipping filter
        if (CurrentFilters.FreeShipping)
        {
            FilteredProducts = FilteredProducts
                .Where(p => p.Price >= 50000)
                .ToList();
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"✓ Final filtered products: {FilteredProducts.Count}",
            LogLevel.Info
        );

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
        await MID_HelperFunctions.DebugMessageAsync($"🛒 Add to cart: {productId}", LogLevel.Info);
    }

    private async Task HandleToggleFavorite(int productId)
    {
        await MID_HelperFunctions.DebugMessageAsync($"❤️ Toggle favorite: {productId}", LogLevel.Info);
    }

    private async Task ResetFilters()
    {
        await MID_HelperFunctions.DebugMessageAsync(
            "🔄 Resetting filters",
            LogLevel.Info
        );
        
        await ShopState.ResetFiltersAsync();
        CurrentFilters = FilterState.CreateDefault();
        SelectedSort = "default";
        CurrentPage = 1;
        
        await ApplyFilters();
        StateHasChanged();
    }

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
        ShopState.OnSearchChanged -= HandleSearchChanged;
        ShopState.OnFiltersChanged -= HandleFiltersChangedFromState;
    }
}