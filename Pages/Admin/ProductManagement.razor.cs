// Pages/Admin/ProductManagement.razor.cs - FIXED
using Microsoft.AspNetCore.Components;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Domain.Product;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Components.Shared.Popups;
using SubashaVentures.Components.Shared.Notifications;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.ObjectPooling;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Models.Firebase;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Services.SupaBase;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Admin;

public partial class ProductManagement : ComponentBase, IAsyncDisposable
{
    [Inject] private IProductService ProductService { get; set; } = default!;
    [Inject] private ISupabaseDatabaseService SupabaseDatabaseService { get; set; } = default!;
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
    private bool showDeleteConfirmation = false;

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

    // Stock management
    private ProductViewModel? selectedProductForStock = null;
    private string newStockQuantity = "";

    // Delete confirmation
    private ProductViewModel? productToDelete = null;
    private List<string>? productsToDelete = null;

    // Component references
    private DynamicModal? productModal;
    private DynamicModal? stockModal;
    private ImageSelectorPopup? imageSelectorPopup;
    private ConfirmationPopup? deleteConfirmationPopup;
    private NotificationComponent? notificationComponent;

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
            ShowErrorNotification("Failed to initialize product management");
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
            ShowErrorNotification("Failed to load products");
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
            
            if (categoryModels != null && categoryModels.Any())
            {
                categories = categoryModels
                    .Where(c => c.IsActive)
                    .Select(c => new CategoryViewModel
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
                    })
                    .OrderBy(c => c.DisplayOrder)
                    .ToList();

                await MID_HelperFunctions.DebugMessageAsync(
                    $"Loaded {categories.Count} categories from Firebase",
                    LogLevel.Info
                );
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No categories found in Firebase",
                    LogLevel.Warning
                );
                categories = new List<CategoryViewModel>();
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading categories from Firebase");
            ShowErrorNotification("Failed to load categories");
            categories = new List<CategoryViewModel>();
        }
    }

    private void ApplyFiltersAndSort()
    {
        using var pooledList = _productListPool?.GetPooled();
        var tempList = pooledList?.Object ?? new List<ProductViewModel>();

        tempList.AddRange(allProducts.Where(p =>
            (string.IsNullOrEmpty(searchQuery) ||
             p.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
             p.Description.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
             p.Sku.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(selectedCategory) || p.CategoryId == selectedCategory) &&
            (string.IsNullOrEmpty(selectedStockStatus) || FilterByStockStatus(p, selectedStockStatus)) &&
            (string.IsNullOrEmpty(selectedStatus) || FilterByStatus(p, selectedStatus))
        ));

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
        isProductModalOpen = true;
        StateHasChanged();
    }

    private void OpenEditProductModal(ProductViewModel product)
    {
        isEditMode = true;
        productForm = MapToFormData(product);
        validationErrors.Clear();
        isProductModalOpen = true;
        StateHasChanged();
    }
    private void CloseProductModal()
    {
        isProductModalOpen = false;
        productForm = new ProductFormData();
        validationErrors.Clear();
        StateHasChanged();
    }

    private void GenerateSku()
    {
        productForm.Sku = ProductService.GenerateUniqueSku();
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
                    ShowSuccessNotification($"Product '{productForm.Name}' updated successfully!");
                }
                else
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "Failed to update product",
                        LogLevel.Error
                    );
                    ShowErrorNotification("Failed to update product");
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
                    ShowSuccessNotification($"Product '{productForm.Name}' created successfully!");
                }
                else
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "Failed to create product",
                        LogLevel.Error
                    );
                    ShowErrorNotification("Failed to create product");
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
            ShowErrorNotification($"Error: {ex.Message}");
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

        if (productForm.Stock < 0)
        {
            validationErrors["Stock"] = "Stock cannot be negative";
        }

        return !validationErrors.Any();
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

    private void HandleDeleteProduct(ProductViewModel product)
    {
        productToDelete = product;
        productsToDelete = null;
        showDeleteConfirmation = true;
        StateHasChanged();
    }

    private async Task ConfirmDeleteProduct()
    {
        if (productToDelete != null)
        {
            try
            {
                var success = await ProductService.DeleteProductAsync(productToDelete.Id);
                
                if (success)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"Product deleted: {productToDelete.Name}",
                        LogLevel.Info
                    );
                    ShowSuccessNotification($"Product '{productToDelete.Name}' deleted successfully!");
                    await LoadProductsAsync();
                    CalculateStats();
                }
                else
                {
                    ShowErrorNotification("Failed to delete product");
                }
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, "Deleting product");
                ShowErrorNotification($"Error deleting product: {ex.Message}");
            }
            finally
            {
                showDeleteConfirmation = false;
                productToDelete = null;
                StateHasChanged();
            }
        }
    }

    private async Task HandleDuplicateProduct(ProductViewModel product)
    {
        try
        {
            var duplicateForm = MapToFormData(product);
            duplicateForm.Id = "";
            duplicateForm.Name = $"{product.Name} (Copy)";
            duplicateForm.Sku = ProductService.GenerateUniqueSku();

            var createRequest = MapToCreateRequest(duplicateForm);
            var result = await ProductService.CreateProductAsync(createRequest);

            if (result != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Product duplicated: {duplicateForm.Name}",
                    LogLevel.Info
                );
                ShowSuccessNotification($"Product duplicated as '{duplicateForm.Name}'");
                await LoadProductsAsync();
                CalculateStats();
            }
            else
            {
                ShowErrorNotification("Failed to duplicate product");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Duplicating product");
            ShowErrorNotification($"Error duplicating product: {ex.Message}");
        }
    }

    private void HandlePreviewProduct(ProductViewModel product)
    {
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
        {
            ShowErrorNotification("Invalid stock quantity");
            return;
        }

        if (quantity < 0)
        {
            ShowErrorNotification("Stock quantity cannot be negative");
            return;
        }

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
                ShowSuccessNotification($"Stock updated to {quantity} units");
                await LoadProductsAsync();
                CalculateStats();
                CloseStockModal();
            }
            else
            {
                ShowErrorNotification("Failed to update stock");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Updating stock");
            ShowErrorNotification($"Error updating stock: {ex.Message}");
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
                ShowSuccessNotification($"Product {(isActive ? "activated" : "deactivated")}");
                await LoadProductsAsync();
                CalculateStats();
            }
            else
            {
                ShowErrorNotification("Failed to update product status");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Toggling active status");
            ShowErrorNotification("Error updating product status");
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
                ShowSuccessNotification($"Product {(isFeatured ? "featured" : "unfeatured")}");
                await LoadProductsAsync();
                CalculateStats();
            }
            else
            {
                ShowErrorNotification("Failed to update featured status");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Toggling featured status");
            ShowErrorNotification("Error updating featured status");
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
        if (!selectedProducts.Any())
        {
            ShowWarningNotification("No products selected");
            return;
        }

        try
        {
            foreach (var productId in selectedProducts)
            {
                var updateRequest = new UpdateProductRequest { IsActive = true };
                await ProductService.UpdateProductAsync(productId, updateRequest);
            }
            
            ShowSuccessNotification($"{selectedProducts.Count} products activated");
            selectedProducts.Clear();
            await LoadProductsAsync();
            CalculateStats();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Bulk activate");
            ShowErrorNotification("Error activating products");
        }
    }

    private async Task HandleBulkDeactivate()
    {
        if (!selectedProducts.Any())
        {
            ShowWarningNotification("No products selected");
            return;
        }

        try
        {
            foreach (var productId in selectedProducts)
            {
                var updateRequest = new UpdateProductRequest { IsActive = false };
                await ProductService.UpdateProductAsync(productId, updateRequest);
            }
            
            ShowSuccessNotification($"{selectedProducts.Count} products deactivated");
            selectedProducts.Clear();
            await LoadProductsAsync();
            CalculateStats();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Bulk deactivate");
            ShowErrorNotification("Error deactivating products");
        }
    }

    private void HandleBulkDelete()
    {
        if (!selectedProducts.Any())
        {
            ShowWarningNotification("No products selected");
            return;
        }

        productsToDelete = new List<string>(selectedProducts);
        productToDelete = null;
        showDeleteConfirmation = true;
        StateHasChanged();
    }

    private async Task ConfirmBulkDelete()
    {
        if (productsToDelete != null && productsToDelete.Any())
        {
            try
            {
                var success = await ProductService.DeleteProductsAsync(productsToDelete);
                
                if (success)
                {
                    ShowSuccessNotification($"{productsToDelete.Count} products deleted");
                    selectedProducts.Clear();
                    await LoadProductsAsync();
                    CalculateStats();
                }
                else
                {
                    ShowErrorNotification("Failed to delete products");
                }
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, "Bulk delete");
                ShowErrorNotification("Error deleting products");
            }
            finally
            {
                showDeleteConfirmation = false;
                productsToDelete = null;
                StateHasChanged();
            }
        }
    }

    private void HandleExport()
    {
        ShowInfoNotification("Export functionality coming soon");
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

    // Notification helpers
    private void ShowSuccessNotification(string message)
    {
        notificationComponent?.ShowSuccess(message);
    }

    private void ShowErrorNotification(string message)
    {
        notificationComponent?.ShowError(message);
    }

    private void ShowWarningNotification(string message)
    {
        notificationComponent?.ShowWarning(message);
    }

    private void ShowInfoNotification(string message)
    {
        notificationComponent?.ShowInfo(message);
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
            Id = string.IsNullOrEmpty(form.Id) ? null : form.Id, // Pass ID if exists
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

    // FIXED: Form data class with "/" separator for tags
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
    
    // FIXED: Use comma separator with proper trimming
    public string TagsInput
    {
        get => Tags != null && Tags.Any() ? string.Join(", ", Tags) : "";
        set => Tags = string.IsNullOrWhiteSpace(value) 
            ? new List<string>() 
            : value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();
    }
    
    public string SizesInput
    {
        get => Sizes != null && Sizes.Any() ? string.Join(", ", Sizes) : "";
        set => Sizes = string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
    }
    
    public string ColorsInput
    {
        get => Colors != null && Colors.Any() ? string.Join(", ", Colors) : "";
        set => Colors = string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .Where(c => !string.IsNullOrEmpty(c))
                .ToList();
    }
}
}