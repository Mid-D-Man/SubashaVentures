using Microsoft.AspNetCore.Components;
using SubashaVentures.Services.Categories;
using SubashaVentures.Services.Brands;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Layout.Shop;

public partial class ShopFilterPanel : ComponentBase
{
    [CascadingParameter] public Pages.Shop.Shop? ShopPage { get; set; }
    [Inject] private ICategoryService CategoryService { get; set; } = null!;
    [Inject] private IBrandService BrandService { get; set; } = null!;

    private List<string> Categories = new();
    private List<string> Brands = new();
    
    private List<string> SelectedCategories = new();
    private List<string> SelectedBrands = new();
    
    private string MinPriceText = "";
    private string MaxPriceText = "";
    
    private int MinRating = 0;
    private bool OnSale = false;
    private bool FreeShipping = false;
    
    private bool IsLoading = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadFilterOptions();
    }

    private async Task LoadFilterOptions()
    {
        IsLoading = true;
        StateHasChanged();

        try
        {
            // Load categories
            var categories = await CategoryService.GetAllCategoriesAsync();
            Categories = categories
                .Select(c => c.Name)
                .OrderBy(n => n)
                .ToList();

            // Load brands
            var brands = await BrandService.GetAllBrandsAsync();
            Brands = brands
                .Select(b => b.Name)
                .OrderBy(n => n)
                .ToList();

            // Fallback if services return empty
            if (!Categories.Any())
            {
                Categories = new List<string> 
                { 
                    "Women Shoes", "Men Shoes", "Lifestyle", 
                    "Skateboarding", "Running", "Sports" 
                };
            }

            if (!Brands.Any())
            {
                Brands = new List<string> 
                { 
                    "Nike", "Adidas", "Puma", "New Balance", 
                    "Converse", "Vans", "Reebok" 
                };
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {Categories.Count} categories and {Brands.Count} brands",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading filter options");
            
            // Fallback
            Categories = new List<string> 
            { 
                "Women Shoes", "Men Shoes", "Lifestyle", 
                "Skateboarding", "Running", "Sports" 
            };
            
            Brands = new List<string> 
            { 
                "Nike", "Adidas", "Puma", "New Balance", 
                "Converse", "Vans", "Reebok" 
            };
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private void ToggleCategory(string category)
    {
        if (SelectedCategories.Contains(category))
            SelectedCategories.Remove(category);
        else
            SelectedCategories.Add(category);
        
        StateHasChanged();
    }

    private void ToggleBrand(string brand)
    {
        if (SelectedBrands.Contains(brand))
            SelectedBrands.Remove(brand);
        else
            SelectedBrands.Add(brand);
        
        StateHasChanged();
    }

    private void ApplyFilters()
    {
        if (ShopPage == null) return;

        var minPrice = 0m;
        var maxPrice = 1000000m;

        if (decimal.TryParse(MinPriceText, out var parsedMin))
            minPrice = parsedMin;

        if (decimal.TryParse(MaxPriceText, out var parsedMax))
            maxPrice = parsedMax;

        var filters = new FilterState
        {
            Categories = SelectedCategories,
            Brands = SelectedBrands,
            MinRating = MinRating,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            OnSale = OnSale,
            FreeShipping = FreeShipping
        };

        _ = MID_HelperFunctions.DebugMessageAsync(
            $"Applying filters: {SelectedCategories.Count} categories, {SelectedBrands.Count} brands",
            LogLevel.Info
        );

        // Directly invoke Shop page method
        ShopPage.HandleFiltersChange(filters);
        
        // Close mobile drawer if open
        ShopPage.CloseMobileFilters();
    }

    private void ResetFilters()
    {
        if (ShopPage == null) return;

        SelectedCategories.Clear();
        SelectedBrands.Clear();
        MinPriceText = "";
        MaxPriceText = "";
        MinRating = 0;
        OnSale = false;
        FreeShipping = false;

        var filters = new FilterState
        {
            Categories = new List<string>(),
            Brands = new List<string>(),
            MinRating = 0,
            MinPrice = 0,
            MaxPrice = 1000000,
            OnSale = false,
            FreeShipping = false
        };

        ShopPage.HandleFiltersChange(filters);
        StateHasChanged();
    }

    public class FilterState
    {
        public List<string> Categories { get; set; } = new();
        public List<string> Brands { get; set; } = new();
        public int MinRating { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public bool OnSale { get; set; }
        public bool FreeShipping { get; set; }
    }
}
