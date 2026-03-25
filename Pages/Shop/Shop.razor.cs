using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
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

    // ==================== DATA ====================

    private List<ProductViewModel> AllProducts { get; set; } = new();
    private List<ProductViewModel> FilteredProducts { get; set; } = new();
    private List<ProductViewModel> CurrentPageProducts { get; set; } = new();

    // Available options extracted from loaded products
    private List<string> AvailableCategories { get; set; } = new();
    private List<string> AvailableBrands { get; set; } = new();

    // ==================== STATE ====================

    private bool IsLoading { get; set; } = true;
    private bool IsInitialized { get; set; } = false;
    private bool HasError { get; set; } = false;
    private string ErrorMessage { get; set; } = "";
    private bool ShowMobileFilters { get; set; } = false;

    private FilterState CurrentFilters { get; set; } = FilterState.CreateDefault();
    private string SelectedSort { get; set; } = "default";

    // ==================== PAGINATION ====================

    private int CurrentPage { get; set; } = 1;
    private int ItemsPerPage { get; set; } = 12;
    private int TotalPages => (int)Math.Ceiling((double)FilteredProducts.Count / ItemsPerPage);

    // ==================== COMPUTED ====================

    private bool HasActiveFilters => CurrentFilters != null && !CurrentFilters.IsEmpty;

    private int ActiveFilterCount
    {
        get
        {
            if (CurrentFilters == null) return 0;
            int count = 0;
            count += CurrentFilters.Categories.Count;
            count += CurrentFilters.SubCategories.Count;
            count += CurrentFilters.Brands.Count;
            if (CurrentFilters.MinRating > 0) count++;
            if (CurrentFilters.MinPrice > 0 || CurrentFilters.MaxPrice < 1000000) count++;
            if (CurrentFilters.OnSale) count++;
            if (CurrentFilters.FreeShipping) count++;
            if (!string.IsNullOrEmpty(CurrentFilters.SearchQuery)) count++;
            return count;
        }
    }

    // ==================== LIFECYCLE ====================

    protected override async Task OnInitializedAsync()
    {
        ShopState.OnSearchChanged += HandleSearchChanged;
        NavigationManager.LocationChanged += OnLocationChanged;

        await LoadProducts();
        ExtractFilterOptions();

        // Parse URL params after products are loaded
        ParseUrlToFilters();

        await ApplyFilters();

        IsInitialized = true;
        StateHasChanged();
    }

    private async void OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        // Only react to /shop URL changes
        if (!e.Location.Contains("/shop")) return;

        ParseUrlToFilters();
        await ApplyFilters();
        await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        ShopState.OnSearchChanged -= HandleSearchChanged;
        NavigationManager.LocationChanged -= OnLocationChanged;
    }

    // ==================== URL HANDLING ====================

    /// <summary>
    /// Reads the current URL query string and populates CurrentFilters.
    /// </summary>
    private void ParseUrlToFilters()
    {
        var uri = new Uri(NavigationManager.Uri);
        var query = QueryHelpers.ParseQuery(uri.Query);

        var filters = FilterState.CreateDefault();

        // Categories (comma-separated)
        if (query.TryGetValue("category", out var cats) && !string.IsNullOrEmpty(cats))
        {
            filters.Categories = cats.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(c => Uri.UnescapeDataString(c.Trim()))
                .ToList();
        }

        // SubCategories
        if (query.TryGetValue("sub", out var subs) && !string.IsNullOrEmpty(subs))
        {
            filters.SubCategories = subs.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => Uri.UnescapeDataString(s.Trim()))
                .ToList();
        }

        // Brands
        if (query.TryGetValue("brand", out var brands) && !string.IsNullOrEmpty(brands))
        {
            filters.Brands = brands.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(b => Uri.UnescapeDataString(b.Trim()))
                .ToList();
        }

        // Search
        if (query.TryGetValue("q", out var q) && !string.IsNullOrEmpty(q))
            filters.SearchQuery = Uri.UnescapeDataString(q.ToString().Trim());

        // Sort
        if (query.TryGetValue("sort", out var sort) && !string.IsNullOrEmpty(sort))
            filters.SortBy = sort.ToString();

        // Min rating
        if (query.TryGetValue("rating", out var rating) &&
            int.TryParse(rating, out var ratingVal))
            filters.MinRating = ratingVal;

        // Price range
        if (query.TryGetValue("minPrice", out var minP) &&
            decimal.TryParse(minP, out var minPVal))
            filters.MinPrice = minPVal;

        if (query.TryGetValue("maxPrice", out var maxP) &&
            decimal.TryParse(maxP, out var maxPVal))
            filters.MaxPrice = maxPVal;

        // Toggles
        if (query.TryGetValue("sale", out var sale))
            filters.OnSale = sale.ToString() == "true";

        if (query.TryGetValue("shipping", out var shipping))
            filters.FreeShipping = shipping.ToString() == "true";

        CurrentFilters = filters;
        SelectedSort = filters.SortBy;

        await MID_HelperFunctions.DebugMessageAsync(
            $"📍 URL parsed → categories:[{string.Join(",", filters.Categories)}] " +
            $"subs:[{string.Join(",", filters.SubCategories)}] q:'{filters.SearchQuery}'",
            LogLevel.Debug
        );
    }

    /// <summary>
    /// Builds a URL from the current filter state and navigates to it.
    /// Uses replace:true so the back button isn't spammed.
    /// </summary>
    private void PushFiltersToUrl()
    {
        var queryParams = new Dictionary<string, string?>();

        if (CurrentFilters.Categories.Any())
            queryParams["category"] = string.Join(",",
                CurrentFilters.Categories.Select(Uri.EscapeDataString));

        if (CurrentFilters.SubCategories.Any())
            queryParams["sub"] = string.Join(",",
                CurrentFilters.SubCategories.Select(Uri.EscapeDataString));

        if (CurrentFilters.Brands.Any())
            queryParams["brand"] = string.Join(",",
                CurrentFilters.Brands.Select(Uri.EscapeDataString));

        if (!string.IsNullOrEmpty(CurrentFilters.SearchQuery))
            queryParams["q"] = Uri.EscapeDataString(CurrentFilters.SearchQuery);

        if (CurrentFilters.SortBy != "default")
            queryParams["sort"] = CurrentFilters.SortBy;

        if (CurrentFilters.MinRating > 0)
            queryParams["rating"] = CurrentFilters.MinRating.ToString();

        if (CurrentFilters.MinPrice > 0)
            queryParams["minPrice"] = CurrentFilters.MinPrice.ToString();

        if (CurrentFilters.MaxPrice < 1000000)
            queryParams["maxPrice"] = CurrentFilters.MaxPrice.ToString();

        if (CurrentFilters.OnSale)
            queryParams["sale"] = "true";

        if (CurrentFilters.FreeShipping)
            queryParams["shipping"] = "true";

        var url = QueryHelpers.AddQueryString("shop", queryParams);
        NavigationManager.NavigateTo(url, replace: true);
    }

    // ==================== PRODUCT LOADING ====================

    private async Task LoadProducts()
    {
        IsLoading = true;
        HasError = false;
        StateHasChanged();

        try
        {
            AllProducts = await ProductService.GetAllProductsAsync();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {AllProducts.Count} products", LogLevel.Info);
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = "Unable to load products. Please try again.";
            await MID_HelperFunctions.LogExceptionAsync(ex, "Shop.LoadProducts");
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private void ExtractFilterOptions()
    {
        AvailableCategories = AllProducts
            .Where(p => !string.IsNullOrWhiteSpace(p.Category))
            .Select(p => p.Category.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        AvailableBrands = AllProducts
            .Where(p => !string.IsNullOrWhiteSpace(p.Brand))
            .Select(p => p.Brand.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(b => b)
            .ToList();
    }

    // ==================== FILTERING ====================

    private async Task ApplyFilters()
    {
        if (!AllProducts.Any())
        {
            FilteredProducts = new List<ProductViewModel>();
            CurrentPageProducts = new List<ProductViewModel>();
            return;
        }

        FilteredProducts = AllProducts
            .Where(p => p.IsActive && !string.IsNullOrEmpty(p.Name))
            .ToList();

        // Search
        if (!string.IsNullOrWhiteSpace(CurrentFilters.SearchQuery))
        {
            var q = CurrentFilters.SearchQuery.ToLowerInvariant().Trim();
            FilteredProducts = FilteredProducts.Where(p =>
                (p.Name?.ToLowerInvariant().Contains(q) ?? false) ||
                (p.Description?.ToLowerInvariant().Contains(q) ?? false) ||
                (p.Brand?.ToLowerInvariant().Contains(q) ?? false) ||
                (p.Category?.ToLowerInvariant().Contains(q) ?? false)
            ).ToList();
        }

        // Category (case-insensitive)
        if (CurrentFilters.Categories.Any())
        {
            FilteredProducts = FilteredProducts.Where(p =>
                !string.IsNullOrEmpty(p.Category) &&
                CurrentFilters.Categories.Any(fc =>
                    p.Category.Equals(fc, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        // SubCategory (case-insensitive)
        if (CurrentFilters.SubCategories.Any())
        {
            FilteredProducts = FilteredProducts.Where(p =>
                !string.IsNullOrEmpty(p.SubCategory) &&
                CurrentFilters.SubCategories.Any(fs =>
                    p.SubCategory.Equals(fs, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        // Brand
        if (CurrentFilters.Brands.Any())
        {
            FilteredProducts = FilteredProducts.Where(p =>
                !string.IsNullOrEmpty(p.Brand) &&
                CurrentFilters.Brands.Any(fb =>
                    p.Brand.Equals(fb, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        // Rating
        if (CurrentFilters.MinRating > 0)
            FilteredProducts = FilteredProducts
                .Where(p => p.Rating >= CurrentFilters.MinRating).ToList();

        // Price
        if (CurrentFilters.MinPrice > 0 || CurrentFilters.MaxPrice < 1000000)
            FilteredProducts = FilteredProducts
                .Where(p => p.Price >= CurrentFilters.MinPrice &&
                            p.Price <= CurrentFilters.MaxPrice).ToList();

        // On Sale
        if (CurrentFilters.OnSale)
            FilteredProducts = FilteredProducts.Where(p => p.IsOnSale).ToList();

        // Free Shipping
        if (CurrentFilters.FreeShipping)
            FilteredProducts = FilteredProducts.Where(p => p.HasFreeShipping).ToList();

        // Sort
        FilteredProducts = SelectedSort switch
        {
            "price-asc"    => FilteredProducts.OrderBy(p => p.Price).ToList(),
            "price-desc"   => FilteredProducts.OrderByDescending(p => p.Price).ToList(),
            "rating-desc"  => FilteredProducts.OrderByDescending(p => p.Rating).ToList(),
            "name-asc"     => FilteredProducts.OrderBy(p => p.Name).ToList(),
            "newest"       => FilteredProducts.OrderByDescending(p => p.CreatedAt).ToList(),
            _              => FilteredProducts.OrderBy(p => p.Id).ToList()
        };

        await MID_HelperFunctions.DebugMessageAsync(
            $"✅ Filtered: {FilteredProducts.Count} products", LogLevel.Debug);

        CurrentPage = 1;
        UpdateCurrentPageProducts();
    }

    private void UpdateCurrentPageProducts()
    {
        CurrentPageProducts = FilteredProducts
            .Skip((CurrentPage - 1) * ItemsPerPage)
            .Take(ItemsPerPage)
            .ToList();

        StateHasChanged();
    }

    // ==================== EVENT HANDLERS ====================

    private async Task HandleFiltersChanged(FilterState filters)
    {
        CurrentFilters = filters.Clone();
        SelectedSort = CurrentFilters.SortBy;
        PushFiltersToUrl();
        // OnLocationChanged will fire and re-apply — but since we already
        // have the filters in memory just apply directly to avoid a round trip
        await ApplyFilters();
        CloseMobileFilters();
    }

    private async Task HandleSearchChanged(string query)
    {
        CurrentFilters.SearchQuery = query ?? "";
        PushFiltersToUrl();
        await ApplyFilters();
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleSortChanged()
    {
        CurrentFilters.SortBy = SelectedSort;
        PushFiltersToUrl();
        await ApplyFilters();
    }

    private async Task ClearFilters()
    {
        CurrentFilters = FilterState.CreateDefault();
        SelectedSort = "default";
        NavigationManager.NavigateTo("shop", replace: true);
        await ApplyFilters();
    }

    private async Task HandleAddToCart(int productId)
    {
        await MID_HelperFunctions.DebugMessageAsync($"🛒 Add to cart: {productId}", LogLevel.Info);
    }

    private async Task HandleToggleFavorite(int productId)
    {
        await MID_HelperFunctions.DebugMessageAsync($"❤️ Toggle favorite: {productId}", LogLevel.Info);
    }

    // ==================== PAGINATION ====================

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

    // ==================== MOBILE FILTERS ====================

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
}
