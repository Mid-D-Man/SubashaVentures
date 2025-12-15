using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Categories;
using SubashaVentures.Services.Brands;
using SubashaVentures.Services.Navigation;
using SubashaVentures.Services.Storage;
using SubashaVentures.Domain.Product;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SubashaVentures.Pages.Shop
{
    public partial class Shop : ComponentBase, IDisposable
    {
        [Inject] private IProductService ProductService { get; set; } = default!;
        [Inject] private ICategoryService CategoryService { get; set; } = default!;
        [Inject] private IBrandService BrandService { get; set; } = default!;
        [Inject] private INavigationService NavigationService { get; set; } = default!;
        [Inject] private IBlazorAppLocalStorageService LocalStorage { get; set; } = default!;
        [Inject] private NavigationManager Navigation { get; set; } = default!;
        [Inject] private ILogger<Shop> Logger { get; set; } = default!;

        [Parameter] public string? Category { get; set; }

        private const string FILTERS_KEY = "shop_filters";
        private const string SORT_KEY = "shop_sort";
        private const string VIEW_MODE_KEY = "shop_view_mode";

        private List<ProductViewModel> allProducts = new();
        private List<ProductViewModel> products = new();
        private List<ProductViewModel> paginatedProducts = new();
        private List<CategoryViewModel> categories = new();
        private List<string> brands = new();
        private HashSet<int> wishlistedProductIds = new();
        private List<FilterTag> activeFilters = new();

        private int currentPage = 1;
        private int itemsPerPage = 24;
        private int totalPages = 1;
        private string viewMode = "grid";
        private string sortBy = "relevance";
        private bool isLoading = true;
        private string searchQuery = "";

        private int TotalProducts => products.Count;

        protected override async Task OnInitializedAsync()
        {
            NavigationService.SearchQueryChanged += OnSearchQueryChanged;
            NavigationService.FiltersChanged += OnFiltersChanged;

            await LoadSavedPreferencesAsync();
            await LoadDataAsync();
        }

        protected override async Task OnParametersSetAsync()
        {
            await LoadProductsAsync();
        }

        private async Task LoadSavedPreferencesAsync()
        {
            try
            {
                sortBy = await LocalStorage.GetItemAsync<string>(SORT_KEY) ?? "relevance";
                var savedViewMode = await LocalStorage.GetItemAsync<string>(VIEW_MODE_KEY);
                viewMode = !string.IsNullOrEmpty(savedViewMode) ? savedViewMode : "grid";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load saved preferences");
            }
        }

        private async Task LoadDataAsync()
        {
            isLoading = true;
            StateHasChanged();

            try
            {
                categories = await CategoryService.GetTopLevelCategoriesAsync();
                
                var brandModels = await BrandService.GetAllBrandsAsync();
                brands = brandModels
                    .Where(b => b.IsActive)
                    .Select(b => b.Name)
                    .Distinct()
                    .OrderBy(b => b)
                    .ToList();

                await LoadProductsAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load shop data");
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private async Task LoadProductsAsync()
        {
            try
            {
                var categorySlug = NavigationService.GetQueryParameter("category") ?? Category ?? "all";
                searchQuery = NavigationService.GetQueryParameter("search") ?? "";

                if (categorySlug == "all")
                {
                    allProducts = await ProductService.GetAllProductsAsync();
                }
                else
                {
                    await ApplyCategoryFilterAsync(categorySlug);
                }

                products = new List<ProductViewModel>(allProducts);

                if (!string.IsNullOrWhiteSpace(searchQuery))
                {
                    ApplySearchFilter(searchQuery);
                }

                ApplyActiveFilters();
                ApplySort();
                CalculatePagination();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load products");
                products = new List<ProductViewModel>();
            }
        }

        private async Task ApplyCategoryFilterAsync(string categorySlug)
        {
            try
            {
                var category = await CategoryService.GetCategoryBySlugAsync(categorySlug);
                if (category != null)
                {
                    allProducts = await ProductService.GetProductsByCategoryAsync(category.Id);
                }
                else
                {
                    allProducts = (await ProductService.GetAllProductsAsync())
                        .Where(p => p.Category?.Equals(categorySlug, StringComparison.OrdinalIgnoreCase) ?? false)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to apply category filter for {Slug}", categorySlug);
                allProducts = new List<ProductViewModel>();
            }
        }

        private void ApplySearchFilter(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;

            var searchTerms = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            products = products.Where(p =>
                searchTerms.Any(term =>
                    (p.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.Brand?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.Category?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (p.Tags?.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase)) ?? false)
                )
            ).ToList();
        }

        private void ApplyActiveFilters()
        {
            if (!activeFilters.Any()) return;

            foreach (var filter in activeFilters)
            {
                switch (filter.FilterType)
                {
                    case FilterType.PriceRange:
                        if (filter.MinPrice.HasValue)
                        {
                            products = products.Where(p => p.Price >= filter.MinPrice.Value).ToList();
                        }
                        if (filter.MaxPrice.HasValue)
                        {
                            products = products.Where(p => p.Price <= filter.MaxPrice.Value).ToList();
                        }
                        break;

                    case FilterType.Rating:
                        if (filter.MinRating.HasValue)
                        {
                            products = products.Where(p => p.Rating >= filter.MinRating.Value).ToList();
                        }
                        break;

                    case FilterType.Brand:
                        if (!string.IsNullOrEmpty(filter.BrandName))
                        {
                            products = products.Where(p => 
                                p.Brand?.Equals(filter.BrandName, StringComparison.OrdinalIgnoreCase) ?? false
                            ).ToList();
                        }
                        break;

                    case FilterType.InStock:
                        products = products.Where(p => p.IsInStock).ToList();
                        break;

                    case FilterType.OnSale:
                        products = products.Where(p => p.IsOnSale).ToList();
                        break;
                }
            }
        }

        private void ApplySort()
        {
            products = sortBy switch
            {
                "price-low" => products.OrderBy(p => p.Price).ToList(),
                "price-high" => products.OrderByDescending(p => p.Price).ToList(),
                "rating" => products.OrderByDescending(p => p.Rating).ThenByDescending(p => p.ReviewCount).ToList(),
                "newest" => products.OrderByDescending(p => p.CreatedAt).ToList(),
                "popular" => products.OrderByDescending(p => p.ViewCount).ThenByDescending(p => p.SalesCount).ToList(),
                "name-asc" => products.OrderBy(p => p.Name).ToList(),
                "name-desc" => products.OrderByDescending(p => p.Name).ToList(),
                _ => products
            };
        }

        private void CalculatePagination()
        {
            totalPages = (int)Math.Ceiling(products.Count / (double)itemsPerPage);
            currentPage = Math.Max(1, Math.Min(currentPage, Math.Max(1, totalPages)));

            var skip = (currentPage - 1) * itemsPerPage;
            paginatedProducts = products.Skip(skip).Take(itemsPerPage).ToList();
        }

        public void AddFilter(FilterTag filter)
        {
            var existingFilter = activeFilters.FirstOrDefault(f => f.Id == filter.Id);
            if (existingFilter != null)
            {
                activeFilters.Remove(existingFilter);
            }

            activeFilters.Add(filter);
            currentPage = 1;
            _ = LoadProductsAsync();
        }

        public void RemoveFilter(FilterTag filter)
        {
            activeFilters.Remove(filter);
            currentPage = 1;
            _ = LoadProductsAsync();
        }

        public void RemoveFilter(string filterId)
        {
            activeFilters.RemoveAll(f => f.Id == filterId);
            currentPage = 1;
            _ = LoadProductsAsync();
        }

        public void ClearAllFilters()
        {
            activeFilters.Clear();
            currentPage = 1;
            _ = LoadProductsAsync();
        }

        private void ResetFilters()
        {
            ClearAllFilters();
            searchQuery = "";
            NavigationService.ClearSearchQuery();
        }

        private async Task HandleSortChange(ChangeEventArgs e)
        {
            sortBy = e.Value?.ToString() ?? "relevance";
            ApplySort();
            CalculatePagination();
            
            await LocalStorage.SetItemAsync(SORT_KEY, sortBy);
            StateHasChanged();
        }

        private async Task SetViewMode(string mode)
        {
            viewMode = mode;
            await LocalStorage.SetItemAsync(VIEW_MODE_KEY, viewMode);
            StateHasChanged();
        }

        private void HandleProductClick(ProductViewModel product)
        {
            Navigation.NavigateTo($"/product/{product.Slug}");
        }

        private void HandleQuickView(ProductViewModel product)
        {
            // TODO: Implement quick view modal
            Logger.LogInformation("Quick view: {ProductName}", product.Name);
        }

        private void HandleAddToCart(ProductViewModel product)
        {
            // TODO: Implement add to cart
            Logger.LogInformation("Add to cart: {ProductName}", product.Name);
        }

        private void HandleWishlistToggle(ProductViewModel product)
        {
            if (wishlistedProductIds.Contains(product.Id))
            {
                wishlistedProductIds.Remove(product.Id);
            }
            else
            {
                wishlistedProductIds.Add(product.Id);
            }
            StateHasChanged();
        }

        private void PreviousPage()
        {
            if (currentPage > 1)
            {
                currentPage--;
                CalculatePagination();
                StateHasChanged();
            }
        }

        private void NextPage()
        {
            if (currentPage < totalPages)
            {
                currentPage++;
                CalculatePagination();
                StateHasChanged();
            }
        }

        private void GoToPage(int page)
        {
            if (page >= 1 && page <= totalPages)
            {
                currentPage = page;
                CalculatePagination();
                StateHasChanged();
            }
        }

        private string GetCategoryDisplayName(string slug)
        {
            var category = categories.FirstOrDefault(c => 
                c.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
            return category?.Name ?? slug;
        }

        private async void OnSearchQueryChanged(object? sender, string query)
        {
            currentPage = 1;
            await LoadProductsAsync();
        }

        private async void OnFiltersChanged(object? sender, EventArgs e)
        {
            currentPage = 1;
            await LoadProductsAsync();
        }

        public void Dispose()
        {
            NavigationService.SearchQueryChanged -= OnSearchQueryChanged;
            NavigationService.FiltersChanged -= OnFiltersChanged;
        }

        public enum FilterType
        {
            PriceRange,
            Rating,
            Brand,
            InStock,
            OnSale
        }

        public class FilterTag
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string DisplayText { get; set; } = string.Empty;
            public FilterType FilterType { get; set; }
            public decimal? MinPrice { get; set; }
            public decimal? MaxPrice { get; set; }
            public float? MinRating { get; set; }
            public string? BrandName { get; set; }
        }
    }
}
