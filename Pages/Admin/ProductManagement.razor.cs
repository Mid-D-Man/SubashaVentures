// Pages/Admin/ProductManagement.razor.cs
using Microsoft.AspNetCore.Components;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Domain.Product;
using SubashaVentures.Models.Firebase;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Components.Shared.Popups;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.ObjectPooling;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Admin;

public partial class ProductManagement : ComponentBase, IAsyncDisposable
{
    [Inject] private IProductService ProductService { get; set; } = default!;
    [Inject] private IFirestoreService FirestoreService { get; set; } = default!;
    [Inject] private ILogger<ProductManagement> Logger { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    // Object pools for performance
    private MID_ComponentObjectPool<List<ProductViewModel>>? _productListPool;
    private MID_ComponentObjectPool<ProductFormData>? _formDataPool;

    // Component state
    private bool isLoading = true;
    private bool isProductModalOpen = false;
    private bool isStockModalOpen = false;
    private bool isImageSelectorOpen = false;
    private bool isEditMode = false;
    private bool isSaving = false;

    private string viewMode = "grid";
    private string searchQuery = "";
    private string selectedCategory = "";
    private string selectedStockStatus = "";
    private string selectedStatus = "";
    private string sortBy = "newest";

    private int currentPage = 1;
    private int pageSize = 24;
    private int lowStockThreshold = 10;

    // Stats
    private int totalProducts = 0;
    private int activeProducts = 0;
    private int outOfStockProducts = 0;
    private int featuredProducts = 0;

    // Data
    private List<ProductViewModel> allProducts = new();
    private List<ProductViewModel> filteredProducts = new();
    private List<ProductViewModel> paginatedProducts = new();
    private List<CategoryViewModel> categories = new();
    private List<string> selectedProducts = new();

    // Form state
    private ProductFormData productForm = new();
    private Dictionary<string, string> validationErrors = new();
    private string priceInput = "";
    private string originalPriceInput = "";
    private string stockInput = "";
    private string tagsInput = "";
    private string sizesInput = "";
    private string colorsInput = "";

    // Stock management
    private ProductViewModel? selectedProductForStock = null;
    private string newStockQuantity = "";

    // Component references
    private DynamicModal? productModal;
    private DynamicModal? stockModal;
    private ImageSelectorPopup? imageSelectorPopup;

    private int totalPages => (int)Math.Ceiling(filteredProducts.Count / (double)pageSize);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Initialize object pools
            _productListPool = new MID_ComponentObjectPool<List<ProductViewModel>>(
                () => new List<ProductViewModel>(),
                list => list.Clear(),
                maxPoolSize: 10
            );

            _formDataPool = new MID_ComponentObjectPool<ProductFormData>(
                () => new ProductFormData(),
                form => ResetFormData(form),
                maxPoolSize: 5
            );

            await MID_HelperFunctions.DebugMessageAsync("ProductManagement initialized", LogLevel.Info);

            // Load initial data
            await Task.WhenAll(
                LoadProductsAsync(),
                LoadCategoriesAsync()
            );

            CalculateStats();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "ProductManagement initialization");
            isLoading = false;
        }
    }

    private async Task LoadProductsAsync()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            var products = await ProductService.GetProductsAsync(0, 1000);
            allProducts = products ?? new List<ProductViewModel>();
            totalProducts = allProducts.Count;

            ApplyFiltersAndSort();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded {totalProducts} products successfully",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading products");
            Logger.LogError(ex, "Failed to load products");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var categoryModels = await FirestoreService.GetCollectionAsync<CategoryModel>("categories");
            categories = categoryModels?.Select(c => new CategoryViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                ImageUrl = c.ImageUrl,
                IconEmoji = c.IconEmoji,
                ParentId = c.ParentId,
                ProductCount = c.ProductCount,
                DisplayOrder = c.DisplayOrder,
                IsActive = c.IsActive
            }).OrderBy(c => c.DisplayOrder).ToList() ?? new List<CategoryViewModel>();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded {categories.Count} categories",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading categories");
        }
    }

    private void ApplyFiltersAndSort()
    {
        using var pooledList = _productListPool?.GetPooled();
        var tempList = pooledList?.Object ?? new List<ProductViewModel>();

        // Apply filters
        tempList.AddRange(allProducts.Where(p =>
            (string.IsNullOrEmpty(searchQuery) ||
             p.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
             p.Description.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
             p.Sku.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(selectedCategory) || p.CategoryId == selectedCategory) &&
            (string.IsNullOrEmpty(selectedStockStatus) || FilterByStockStatus(p, selectedStockStatus)) &&
            (string.IsNullOrEmpty(selectedStatus) || FilterByStatus(p, selectedStatus))
        ));

        // Apply sorting
        filteredProducts = sortBy switch
        {
            "newest" => tempList.OrderByDescending(x => x.CreatedAt).ToList(),
            "oldest" => tempList.OrderBy(x => x.CreatedAt).ToList(),
            "name-az" => tempList.OrderBy(x => x.Name).ToList(),
            "name-za" => tempList.OrderByDescending(x => x.Name).ToList(),
            "price-high" => tempList.OrderByDescending(x => x.Price).ToList(),
            "price-low" => tempList.OrderBy(x => x.Price).ToList(),
            "stock-high" => tempList.OrderByDescending(x => x.Stock).ToList(),
            "stock-low" => tempList.OrderBy(x => x.Stock).ToList(),
            _ => tempList
        };

        currentPage = 1;
        UpdatePaginatedProducts();
    }

    private bool FilterByStockStatus(ProductViewModel product, string status)
    {
        return status switch
        {
            "in-stock" => product.Stock > lowStockThreshold,
            "low-stock" => product.Stock > 0 && product.Stock <= lowStockThreshold,
            "out-of-stock" => product.Stock == 0,
            _ => true
        };
    }

    private bool FilterByStatus(ProductViewModel product, string status)
    {
        return status switch
        {
            "active" => product.IsActive,
            "inactive" => !product.IsActive,
            "featured" => product.IsFeatured,
            "on-sale" => product.IsOnSale,
            _ => true
        };
    }

    private void UpdatePaginatedProducts()
    {
        paginatedProducts = filteredProducts
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        StateHasChanged();
    }

    private void CalculateStats()
    {
        activeProducts = allProducts.Count(p => p.IsActive);
        outOfStockProducts = allProducts.Count(p => p.Stock == 0);
        featuredProducts = allProducts.Count(p => p.IsFeatured);
    }

    private void HandleSearch()
    {
        ApplyFiltersAndSort();
    }

    private void HandleCategoryFilter(ChangeEventArgs e)
    {
        selectedCategory = e.Value?.ToString() ?? "";
        ApplyFiltersAndSort();
    }

    private void HandleStockFilter(ChangeEventArgs e)
    {
        selectedStockStatus = e.Value?.ToString() ?? "";
        ApplyFiltersAndSort();
    }

    private void HandleStatusFilter(ChangeEventArgs e)
    {
        selectedStatus = e.Value?.ToString() ?? "";
        ApplyFiltersAndSort();
    }

    private void HandleSortChange(ChangeEventArgs e)
    {
        sortBy = e.Value?.ToString() ?? "newest";
        ApplyFiltersAndSort();
    }

    private void OpenCreateProductModal()
    {
        isEditMode = false;
        productForm = new ProductFormData();
        validationErrors.Clear();
        ClearFormInputs();
        isProductModalOpen = true;
        StateHasChanged();
    }

    private void OpenEditProductModal(ProductViewModel product)
    {
        isEditMode = true;
        productForm = MapToFormData(product);
        validationErrors.Clear();
        PopulateFormInputs();
        isProductModalOpen = true;
        StateHasChanged();
    }

    private void CloseProductModal()
    {
        isProductModalOpen = false;
        productForm = new ProductFormData();
        validationErrors.Clear();
        ClearFormInputs();
        StateHasChanged();
    }

    private async Task HandleSaveProduct()
    {
        if (!ValidateProductForm())
        {
            StateHasChanged();
            return;
        }

        try
        {
            isSaving = true;
            StateHasChanged();

            // Parse form inputs
            ParseFormInputs();

            if (isEditMode)
            {
                var updateRequest = MapToUpdateRequest(productForm);
                var success = await ProductService.UpdateProductAsync(productForm.Id, updateRequest);

                if (success)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"Product updated: {productForm.Name}",
                        LogLevel.Info
                    );
                    // TODO: Show success toast
                }
                else
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "Failed to update product",
                        LogLevel.Error
                    );
                    // TODO: Show error toast
                    return;
                }
            }
            else
            {
                var createRequest = MapToCreateRequest(productForm);
                var result = await ProductService.CreateProductAsync(createRequest);

                if (result != null)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"Product created: {productForm.Name}",
                        LogLevel.Info
                    );
                    // TODO: Show success toast
                }
                else
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "Failed to create product",
                        LogLevel.Error
                    );
                    // TODO: Show error toast
                    return;
                }
            }

            CloseProductModal();
            await LoadProductsAsync();
            CalculateStats();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Saving product");
            // TODO: Show error toast
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    private bool ValidateProductForm()
    {
        validationErrors.Clear();

        if (string.IsNullOrWhiteSpace(productForm.Name))
        {
            validationErrors["Name"] = "Product name is required";
        }

        if (productForm.Price <= 0)
        {
            validationErrors["Price"] = "Price must be greater than 0";
        }

        if (string.IsNullOrWhiteSpace(productForm.Sku))
        {
            validationErrors["Sku"] = "SKU is required";
        }

        if (string.IsNullOrWhiteSpace(productForm.CategoryId))
        {
            validationErrors["CategoryId"] = "Category is required";
        }

        return !validationErrors.Any();
    }

    private void ParseFormInputs()
    {
        if (decimal.TryParse(priceInput, out var price))
            productForm.Price = price;

        if (!string.IsNullOrEmpty(originalPriceInput) && decimal.TryParse(originalPriceInput, out var originalPrice))
            productForm.OriginalPrice = originalPrice;

        if (int.TryParse(stockInput, out var stock))
            productForm.Stock = stock;

        if (!string.IsNullOrEmpty(tagsInput))
            productForm.Tags = tagsInput.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();

        if (!string.IsNullOrEmpty(sizesInput))
            productForm.Sizes = sizesInput.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();

        if (!string.IsNullOrEmpty(colorsInput))
            productForm.Colors = colorsInput.Split(',').Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToList();
    }

    private void PopulateFormInputs()
    {
        priceInput = productForm.Price.ToString("0.00");
        originalPriceInput = productForm.OriginalPrice?.ToString("0.00") ?? "";
        stockInput = productForm.Stock.ToString();
        tagsInput = productForm.Tags != null ? string.Join(", ", productForm.Tags) : "";
        sizesInput = productForm.Sizes != null ? string.Join(", ", productForm.Sizes) : "";
        colorsInput = productForm.Colors != null ? string.Join(", ", productForm.Colors) : "";
    }

    private void ClearFormInputs()
    {
        priceInput = "";
        originalPriceInput = "";
        stockInput = "";
        tagsInput = "";
        sizesInput = "";
        colorsInput = "";
    }

    private int CalculateDiscount()
    {
        if (productForm.OriginalPrice.HasValue && productForm.OriginalPrice > productForm.Price)
        {
            return (int)Math.Round(((productForm.OriginalPrice.Value - productForm.Price) / productForm.OriginalPrice.Value) * 100);
        }
        return 0;
    }

    private void OpenImageSelector()
    {
        isImageSelectorOpen = true;
        StateHasChanged();
    }

    private void CloseImageSelector()
    {
        isImageSelectorOpen = false;
        StateHasChanged();
    }

    private void HandleImagesSelected(List<string> selectedImageUrls)
    {
        productForm.ImageUrls = selectedImageUrls;
        CloseImageSelector();
        StateHasChanged();
    }

    private void RemoveImage(string imageUrl)
    {
        if (productForm.ImageUrls != null)
        {
            productForm.ImageUrls.Remove(imageUrl);
            StateHasChanged();
        }
    }

    private async Task HandleDeleteProduct(ProductViewModel product)
    {
        // TODO: Show confirmation modal
        try
        {
            var success = await ProductService.DeleteProductAsync(product.Id);
            
            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Product deleted: {product.Name}",
                    LogLevel.Info
                );
                await LoadProductsAsync();
                CalculateStats();
                // TODO: Show success toast
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Deleting product");
            // TODO: Show error toast
        }
    }

    private async Task HandleDuplicateProduct(ProductViewModel product)
    {
        try
        {
            var duplicateForm = MapToFormData(product);
            duplicateForm.Id = "";
            duplicateForm.Name = $"{product.Name} (Copy)";
            duplicateForm.Sku = $"{product.Sku}-COPY";

            var createRequest = MapToCreateRequest(duplicateForm);
            var result = await ProductService.CreateProductAsync(createRequest);

            if (result != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Product duplicated: {duplicateForm.Name}",
                    LogLevel.Info
                );
                await LoadProductsAsync();
                CalculateStats();
                // TODO: Show success toast
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Duplicating product");
            // TODO: Show error toast
        }
    }

    private void HandlePreviewProduct(ProductViewModel product)
    {
        // TODO: Open preview modal or navigate to product details
        NavigationManager.NavigateTo($"/product/{product.Slug}");
    }

    private void OpenStockModal(ProductViewModel product)
    {
        selectedProductForStock = product;
        newStockQuantity = product.Stock.ToString();
        isStockModalOpen = true;
        StateHasChanged();
    }

    private void CloseStockModal()
    {
        isStockModalOpen = false;
        selectedProductForStock = null;
        newStockQuantity = "";
        StateHasChanged();
    }

    private async Task HandleUpdateStock()
    {
        if (selectedProductForStock == null || !int.TryParse(newStockQuantity, out var quantity))
            return;

        try
        {
            isSaving = true;
            StateHasChanged();

            var success = await ProductService.UpdateProductStockAsync(selectedProductForStock.Id, quantity);

            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Stock updated for: {selectedProductForStock.Name}",
                    LogLevel.Info
                );
                await LoadProductsAsync();
                CalculateStats();
                CloseStockModal();
                // TODO: Show success toast
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Updating stock");
            // TODO: Show error toast
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    private async Task HandleToggleActive(ProductViewModel product, bool isActive)
    {
        try
        {
            var updateRequest = new UpdateProductRequest { IsActive = isActive };
            var success = await ProductService.UpdateProductAsync(product.Id, updateRequest);

            if (success)
            {
                await LoadProductsAsync();
                CalculateStats();
                // TODO: Show success toast
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Toggling active status");
        }
    }

    private async Task HandleToggleFeatured(ProductViewModel product, bool isFeatured)
    {
        try
        {
            var updateRequest = new UpdateProductRequest { IsFeatured = isFeatured };
            var success = await ProductService.UpdateProductAsync(product.Id, updateRequest);

            if (success)
            {
                await LoadProductsAsync();
                CalculateStats();
                // TODO: Show success toast
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Toggling featured status");
        }
    }

    private void HandleSelectionChanged(string productId, bool isSelected)
    {
        if (isSelected)
        {
            if (!selectedProducts.Contains(productId))
                selectedProducts.Add(productId);
        }
        else
        {
            selectedProducts.Remove(productId);
        }
        StateHasChanged();
    }

    private void HandleSelectAll(ChangeEventArgs e)
    {
        if (e.Value is bool isChecked)
        {
            if (isChecked)
            {
                selectedProducts = paginatedProducts.Select(p => p.Id).ToList();
            }
            else
            {
                selectedProducts.Clear();
            }
            StateHasChanged();
        }
    }

    private async Task HandleBulkActivate()
    {
        foreach (var productId in selectedProducts)
        {
            var updateRequest = new UpdateProductRequest { IsActive = true };
            await ProductService.UpdateProductAsync(productId, updateRequest);
        }
        selectedProducts.Clear();
        await LoadProductsAsync();
        CalculateStats();
        // TODO: Show success toast
    }

    private async Task HandleBulkDeactivate()
    {
        foreach (var productId in selectedProducts)
        {
            var updateRequest = new UpdateProductRequest { IsActive = false };
            await ProductService.UpdateProductAsync(productId, updateRequest);
        }
        selectedProducts.Clear();
        await LoadProductsAsync();
        CalculateStats();
        // TODO: Show success toast
    }

    private async Task HandleBulkDelete()
    {
        // TODO: Show confirmation modal
        try
        {
            var success = await ProductService.DeleteProductsAsync(selectedProducts);
            if (success)
            {
                selectedProducts.Clear();
                await LoadProductsAsync();
                CalculateStats();
                // TODO: Show success toast
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Bulk delete");
        }
    }

    private void HandleExport()
    {
        // TODO: Implement CSV export
        MID_HelperFunctions.DebugMessage("Export not yet implemented", LogLevel.Info);
    }

    private string GetStockClass(int stock)
    {
        if (stock <= 0) return "out-of-stock";
        if (stock <= lowStockThreshold) return "low";
        return "in-stock";
    }

    private void PreviousPage()
    {
        if (currentPage > 1)
        {
            currentPage--;
            UpdatePaginatedProducts();
        }
    }

    private void NextPage()
    {
        if (currentPage < totalPages)
        {
            currentPage++;
            UpdatePaginatedProducts();
        }
    }

    private void GoToPage(int page)
    {
        if (page >= 1 && page <= totalPages)
        {
            currentPage = page;
            UpdatePaginatedProducts();
        }
    }

    // Mapping methods
    private ProductFormData MapToFormData(ProductViewModel product)
    {
        return new ProductFormData
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            LongDescription = product.LongDescription,
            Price = product.Price,
            OriginalPrice = product.OriginalPrice,
            Stock = product.Stock,
            Sku = product.Sku,
            CategoryId = product.CategoryId,
            Brand = product.Brand,
            Tags = product.Tags?.ToList(),
            Sizes = product.Sizes?.ToList(),
            Colors = product.Colors?.ToList(),
            ImageUrls = product.Images?.ToList(),
            IsFeatured = product.IsFeatured,
            IsActive = product.IsActive
        };
    }

    private CreateProductRequest MapToCreateRequest(ProductFormData form)
    {
        return new CreateProductRequest
        {
            Name = form.Name,
            Description = form.Description,
            LongDescription = form.LongDescription,
            Price = form.Price,
            OriginalPrice = form.OriginalPrice,
            Stock = form.Stock,
            Sku = form.Sku,
            CategoryId = form.CategoryId,
            Brand = form.Brand,
            Tags = form.Tags,
            Sizes = form.Sizes,
            Colors = form.Colors,
            ImageUrls = form.ImageUrls,
            IsFeatured = form.IsFeatured
        };
    }

    private UpdateProductRequest MapToUpdateRequest(ProductFormData form)
    {
        return new UpdateProductRequest
        {
            Name = form.Name,
            Description = form.Description,
            LongDescription = form.LongDescription,
            Price = form.Price,
            OriginalPrice = form.OriginalPrice,
            Stock = form.Stock,
            CategoryId = form.CategoryId,
            Brand = form.Brand,
            Tags = form.Tags,
            Sizes = form.Sizes,
            Colors = form.Colors,
            IsFeatured = form.IsFeatured,
            IsActive = form.IsActive
        };
    }

    private void ResetFormData(ProductFormData form)
    {
        form.Id = "";
        form.Name = "";
        form.Description = "";
        form.LongDescription = "";
        form.Price = 0;
        form.OriginalPrice = null;
        form.Stock = 0;
        form.Sku = "";
        form.CategoryId = "";
        form.Brand = "";
        form.Tags?.Clear();
        form.Sizes?.Clear();
        form.Colors?.Clear();
        form.ImageUrls?.Clear();
        form.IsFeatured = false;
        form.IsActive = true;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _productListPool?.Dispose();
            _formDataPool?.Dispose();

            await MID_HelperFunctions.DebugMessageAsync(
                "ProductManagement disposed successfully",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error disposing ProductManagement");
        }
    }

    // Form data class
    public class ProductFormData
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string LongDescription { get; set; } = "";
        public decimal Price { get; set; }
        public decimal? OriginalPrice { get; set; }
        public int Stock { get; set; }
        public string Sku { get; set; } = "";
        public string CategoryId { get; set; } = "";
        public string Brand { get; set; } = "";
        public List<string>? Tags { get; set; }
        public List<string>? Sizes { get; set; }
        public List<string>? Colors { get; set; }
        public List<string>? ImageUrls { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
