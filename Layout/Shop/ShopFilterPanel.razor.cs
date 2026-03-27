using Microsoft.AspNetCore.Components;
using SubashaVentures.Domain.Shop;
using SubashaVentures.Domain.Product;
using SubashaVentures.Services.Categories;
using SubashaVentures.Services.Brands;
using SubashaVentures.Services.VisualElements;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Layout.Shop;

public partial class ShopFilterPanel : ComponentBase
{
    [Parameter] public EventCallback<FilterState> OnFiltersChanged { get; set; }
    [Parameter] public FilterState? CurrentFilters { get; set; }

    [Inject] private ICategoryService CategoryService { get; set; } = null!;
    [Inject] private IBrandService BrandService { get; set; } = null!;
    [Inject] private IVisualElementsService VisualElements { get; set; } = null!;

    // ==================== DATA ====================

    private List<CategoryViewModel> CategoriesWithSubs { get; set; } = new();
    private List<string> Brands { get; set; } = new();

    // ==================== SELECTED STATE ====================

    private List<string> SelectedCategories = new();
    private List<string> SelectedSubCategories = new();
    private List<string> SelectedBrands = new();
    private string MinPriceText = "";
    private string MaxPriceText = "";
    private int MinRating = 0;
    private bool OnSale = false;
    private bool FreeShipping = false;

    // ==================== UI STATE ====================

    private bool IsLoading = true;
    public string StarSvg { get; private set; } = string.Empty;

    // Subcategories currently visible — derived from selected main categories
    private List<SubCategoryViewModel> VisibleSubCategories =>
        CategoriesWithSubs
            .Where(c => SelectedCategories.Contains(c.Name, StringComparer.OrdinalIgnoreCase))
            .SelectMany(c => c.SubCategories)
            .OrderByDescending(s => s.IsDefault)
            .ThenBy(s => s.DisplayOrder)
            .ThenBy(s => s.Name)
            .ToList();

    private FilterState? _lastSyncedFilters = null;

    // ==================== LIFECYCLE ====================

    protected override async Task OnInitializedAsync()
    {
        await LoadStarSvgAsync();
        await LoadFilterOptionsAsync();

        if (CurrentFilters != null)
            SyncFromFilters(CurrentFilters);
    }

    protected override void OnParametersSet()
    {
        if (CurrentFilters == null || IsLoading) return;

        // Only sync if filters actually changed
        if (_lastSyncedFilters == null || !FiltersEqual(_lastSyncedFilters, CurrentFilters))
            SyncFromFilters(CurrentFilters);
    }

    // ==================== DATA LOADING ====================

    private async Task LoadFilterOptionsAsync()
    {
        IsLoading = true;
        StateHasChanged();

        try
        {
            var categoriesTask = CategoryService.GetCategoriesWithSubcategoriesAsync();
            var brandsTask = BrandService.GetAllBrandsAsync();

            await Task.WhenAll(categoriesTask, brandsTask);

            CategoriesWithSubs = await categoriesTask;

            Brands = (await brandsTask)
                .Where(b => b.IsActive)
                .Select(b => b.Name.Trim())
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Filter options: {CategoriesWithSubs.Count} categories, {Brands.Count} brands",
                LogLevel.Info);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "ShopFilterPanel.LoadFilterOptions");
            CategoriesWithSubs = new List<CategoryViewModel>();
            Brands = new List<string>();
        }
        finally
        {
            IsLoading = false;

            if (CurrentFilters != null)
                SyncFromFilters(CurrentFilters);

            StateHasChanged();
        }
    }

    private async Task LoadStarSvgAsync()
    {
        try
        {
            StarSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Star, width: 16, height: 16, fillColor: "currentColor");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading star SVG: {ex.Message}");
        }
    }

    // ==================== SYNC ====================

    private void SyncFromFilters(FilterState filters)
    {
        SelectedCategories = new List<string>(filters.Categories);
        SelectedSubCategories = new List<string>(filters.SubCategories);
        SelectedBrands = new List<string>(filters.Brands);
        MinRating = filters.MinRating;
        MinPriceText = filters.MinPrice > 0 ? filters.MinPrice.ToString() : "";
        MaxPriceText = filters.MaxPrice < 1000000 ? filters.MaxPrice.ToString() : "";
        OnSale = filters.OnSale;
        FreeShipping = filters.FreeShipping;

        _lastSyncedFilters = filters.Clone();
        StateHasChanged();
    }

    private bool FiltersEqual(FilterState a, FilterState b)
    {
        return a.Categories.SequenceEqual(b.Categories) &&
               a.SubCategories.SequenceEqual(b.SubCategories) &&
               a.Brands.SequenceEqual(b.Brands) &&
               a.MinRating == b.MinRating &&
               a.MinPrice == b.MinPrice &&
               a.MaxPrice == b.MaxPrice &&
               a.OnSale == b.OnSale &&
               a.FreeShipping == b.FreeShipping &&
               a.SearchQuery == b.SearchQuery &&
               a.SortBy == b.SortBy;
    }

    // ==================== TOGGLE HELPERS ====================

    private void ToggleCategory(string category)
    {
        if (SelectedCategories.Contains(category, StringComparer.Ordinal))
        {
            SelectedCategories.Remove(category);

            // Clear any subcategory selections that belonged to this category
            var removedCat = CategoriesWithSubs
                .FirstOrDefault(c => c.Name.Equals(category, StringComparison.OrdinalIgnoreCase));

            if (removedCat != null)
            {
                var subNames = removedCat.SubCategories.Select(s => s.Name).ToHashSet();
                SelectedSubCategories.RemoveAll(s => subNames.Contains(s));
            }
        }
        else
        {
            SelectedCategories.Add(category);
            // NOTE: No auto-selection of default subcategory — user picks subcategories manually.
            // Auto-selection was causing "No Products Found" for products without a subcategory set.
        }

        StateHasChanged();
    }

    private void ToggleSubCategory(string subName)
    {
        if (SelectedSubCategories.Contains(subName, StringComparer.Ordinal))
            SelectedSubCategories.Remove(subName);
        else
            SelectedSubCategories.Add(subName);

        StateHasChanged();
    }

    private void ToggleBrand(string brand)
    {
        if (SelectedBrands.Contains(brand, StringComparer.Ordinal))
            SelectedBrands.Remove(brand);
        else
            SelectedBrands.Add(brand);

        StateHasChanged();
    }

    private void SetMinRating(int rating)
    {
        MinRating = rating;
        StateHasChanged();
    }

    private void ToggleOnSale()
    {
        OnSale = !OnSale;
        StateHasChanged();
    }

    private void ToggleFreeShipping()
    {
        FreeShipping = !FreeShipping;
        StateHasChanged();
    }

    private bool IsCategoryChecked(string name) =>
        SelectedCategories.Contains(name, StringComparer.Ordinal);

    private bool IsSubChecked(string name) =>
        SelectedSubCategories.Contains(name, StringComparer.Ordinal);

    private bool IsBrandChecked(string name) =>
        SelectedBrands.Contains(name, StringComparer.Ordinal);

    // ==================== APPLY / RESET ====================

    private async Task ApplyFilters()
    {
        decimal.TryParse(MinPriceText, out var minPrice);
        var maxPrice = decimal.TryParse(MaxPriceText, out var maxVal) ? maxVal : 1000000m;

        var filters = new FilterState
        {
            Categories = new List<string>(SelectedCategories),
            SubCategories = new List<string>(SelectedSubCategories),
            Brands = new List<string>(SelectedBrands),
            MinRating = MinRating,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            OnSale = OnSale,
            FreeShipping = FreeShipping,
            SearchQuery = CurrentFilters?.SearchQuery ?? "",
            SortBy = CurrentFilters?.SortBy ?? "default"
        };

        _lastSyncedFilters = filters.Clone();

        if (OnFiltersChanged.HasDelegate)
            await OnFiltersChanged.InvokeAsync(filters);
    }

    private async Task ResetFilters()
    {
        SelectedCategories.Clear();
        SelectedSubCategories.Clear();
        SelectedBrands.Clear();
        MinPriceText = "";
        MaxPriceText = "";
        MinRating = 0;
        OnSale = false;
        FreeShipping = false;

        var filters = FilterState.CreateDefault();
        _lastSyncedFilters = filters.Clone();

        if (OnFiltersChanged.HasDelegate)
            await OnFiltersChanged.InvokeAsync(filters);

        StateHasChanged();
    }
}
