using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SubashaVentures.Services.Products;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.Brands;
using SubashaVentures.Services.Partners;
using SubashaVentures.Services.Categories;
using SubashaVentures.Domain.Product;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Models.Firebase;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Components.Shared.Popups;
using SubashaVentures.Components.Shared.Notifications;
using SubashaVentures.Utilities.HelperScripts;
using SubashaVentures.Utilities.ObjectPooling;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Services.SupaBase;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Admin;

public partial class ProductManagement : ComponentBase, IAsyncDisposable
{
    [Inject] private IProductService ProductService { get; set; } = default!;
    [Inject] private IProductOfTheDayService ProductOfTheDayService { get; set; } = default!;
    [Inject] private ISupabaseDatabaseService SupabaseDatabaseService { get; set; } = default!;
    [Inject] private IFirestoreService FirestoreService { get; set; } = default!;
    [Inject] private IBrandService BrandService { get; set; } = default!;
    [Inject] private IPartnerService PartnerService { get; set; } = default!;
    [Inject] private ICategoryService CategoryService { get; set; } = default!;
    [Inject] private ILogger<ProductManagement> Logger { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    // Object pools
    private MID_ComponentObjectPool<List<ProductViewModel>>? _productListPool;
    private MID_ComponentObjectPool<ProductFormData>? _formDataPool;

    // Component state
    private bool isLoading = true;
    private bool isProductModalOpen = false;
    private bool isVariantModalOpen = false;
    private bool isStockModalOpen = false;
    private bool isImageSelectorOpen = false;
    private bool isEditMode = false;
    private bool isEditingVariant = false;
    private bool isSaving = false;
    private bool showDeleteConfirmation = false;

    private string viewMode = "grid";
    private string searchQuery = "";
    private string selectedCategory = "";
    private string selectedOwnership = "";
    private string selectedStockStatus = "";
    private string selectedStatus = "";
    private string sortBy = "newest";

    private int currentPage = 1;
    private int pageSize = 24;
    private int lowStockThreshold = 10;

    // Stats
    private int totalProducts = 0;
    private int activeProducts = 0;
    private int partnerProducts = 0;
    private int outOfStockProducts = 0;
    private int featuredProducts = 0;

    // Product of the Day
    private ProductViewModel? productOfTheDay = null;
    private DateTime? potdLastUpdate = null;

    // Data
    private List<ProductViewModel> allProducts = new();
    private List<ProductViewModel> filteredProducts = new();
    private List<ProductViewModel> paginatedProducts = new();
    private List<CategoryViewModel> categories = new();
    private List<BrandModel> brands = new();
    private List<PartnerModel> partners = new();
    private List<int> selectedProducts = new();

    // Template selection
    private List<string> commonTags = new();
    private List<string> commonSizes = new();
    private List<string> commonColors = new();

    // Form state
    private ProductFormData productForm = new();
    private VariantFormData variantForm = new();
    private string? editingVariantKey = null;
    private Dictionary<string, string> validationErrors = new();

    // Stock management
    private ProductViewModel? selectedProductForStock = null;

    // Delete confirmation
    private ProductViewModel? productToDelete = null;
    private List<int>? productsToDelete = null;

    // Component references
    private DynamicModal? productModal;
    private DynamicModal? variantModal;
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
                LoadCategoriesAsync(),
                LoadBrandsAsync(),
                LoadPartnersAsync(),
                LoadProductOfTheDayAsync()
            );

            CalculateStats();
            
            commonTags = ProductService.GetCommonTags();
            commonSizes = ProductService.GetCommonSizes();
            commonColors = ProductService.GetCommonColors();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "ProductManagement initialization");
            ShowErrorNotification("Failed to initialize product management");
            isLoading = false;
        }
    }

    // ==================== DATA LOADING ====================

    private async Task LoadPartnersAsync()
    {
        try
        {
            partners = await PartnerService.GetActivePartnersAsync();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded {partners.Count} active partners",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading partners");
            ShowErrorNotification("Failed to load partners");
            partners = new List<PartnerModel>();
        }
    }

    private async Task LoadBrandsAsync()
    {
        try
        {
            brands = await BrandService.GetAllBrandsAsync();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded {brands.Count} brands from Firebase",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading brands");
            ShowErrorNotification("Failed to load brands");
            brands = new List<BrandModel>();
        }
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            categories = await CategoryService.GetAllCategoriesAsync();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {categories.Count} categories from CategoryService",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading categories");
            ShowErrorNotification("Failed to load categories");
            categories = new List<CategoryViewModel>();
        }
    }

    private async Task LoadProductOfTheDayAsync()
    {
        try
        {
            productOfTheDay = await ProductOfTheDayService.GetProductOfTheDayAsync();
            potdLastUpdate = await ProductOfTheDayService.GetLastUpdateTimeAsync();
            
            if (productOfTheDay != null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Product of the Day loaded: {productOfTheDay.Name}",
                    LogLevel.Info
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading Product of the Day");
        }
    }

    private async Task SetAsProductOfTheDay(ProductViewModel product)
    {
        try
        {
            var success = await ProductOfTheDayService.SetProductOfTheDayAsync(product.Id);
            
            if (success)
            {
                await LoadProductOfTheDayAsync();
                ShowSuccessNotification($"'{product.Name}' set as Product of the Day!");
                StateHasChanged();
            }
            else
            {
                ShowErrorNotification("Failed to set Product of the Day. Ensure product is active and in stock.");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Setting Product of the Day");
            ShowErrorNotification("Error setting Product of the Day");
        }
    }

    private async Task RefreshProductOfTheDay()
    {
        try
        {
            var success = await ProductOfTheDayService.AutoSelectProductOfTheDayAsync();
            
            if (success)
            {
                await LoadProductOfTheDayAsync();
                ShowSuccessNotification("Product of the Day refreshed based on performance metrics!");
                StateHasChanged();
            }
            else
            {
                ShowErrorNotification("Failed to refresh Product of the Day");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Refreshing Product of the Day");
            ShowErrorNotification("Error refreshing Product of the Day");
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

            // Load partner names for products
            foreach (var product in allProducts.Where(p => !p.IsOwnedByStore && p.PartnerId.HasValue))
            {
                var partner = partners.FirstOrDefault(p => p.Id == product.PartnerId.Value);
                if (partner != null)
                {
                    product.PartnerName = partner.Name;
                }
            }

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

    // ==================== FILTERING & SORTING ====================

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
            (string.IsNullOrEmpty(selectedOwnership) || FilterByOwnership(p, selectedOwnership)) &&
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

    private bool FilterByOwnership(ProductViewModel product, string ownership)
    {
        return ownership switch
        {
            "owned" => product.IsOwnedByStore,
            "partner" => !product.IsOwnedByStore && product.PartnerId.HasValue,
            _ => true
        };
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
        partnerProducts = allProducts.Count(p => !p.IsOwnedByStore && p.PartnerId.HasValue);
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

    private void HandleOwnershipFilter(ChangeEventArgs e)
    {
        selectedOwnership = e.Value?.ToString() ?? "";
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

    // ==================== PRODUCT MODAL ====================

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

    private void HandleOwnershipChange(ChangeEventArgs e)
    {
        if (e.Value is bool isOwned)
        {
            productForm.IsOwnedByStore = isOwned;
            if (isOwned)
            {
                productForm.PartnerIdString = null;
            }
            StateHasChanged();
        }
    }

    private void GenerateSku()
    {
        productForm.Sku = ProductService.GenerateUniqueSku();
        StateHasChanged();
    }

    // ✅ UPDATED: Generate variant SKU with -VR- prefix to distinguish from main product
    private void GenerateVariantSku()
    {
        if (string.IsNullOrEmpty(productForm.Sku))
        {
            ShowWarningNotification("Please generate main product SKU first");
            return;
        }

        var variantIdentifier = new List<string>();
        if (!string.IsNullOrEmpty(variantForm.Size)) variantIdentifier.Add(variantForm.Size);
        if (!string.IsNullOrEmpty(variantForm.Color)) variantIdentifier.Add(variantForm.Color);

        var suffix = variantIdentifier.Any() 
            ? string.Join("-", variantIdentifier).ToUpperInvariant()
            : Guid.NewGuid().ToString("N").Substring(0, 4).ToUpper();

        // ✅ CHANGE: Added -VR- to make variant SKUs noticeably different
        variantForm.Sku = $"{productForm.Sku}-VR-{suffix}";
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

        if (!productForm.IsOwnedByStore && string.IsNullOrWhiteSpace(productForm.PartnerIdString))
        {
            validationErrors["PartnerId"] = "Partner is required for partner products";
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

    // ==================== VARIANT MODAL ====================

    private void OpenVariantModal()
    {
        isEditingVariant = false;
        editingVariantKey = null;
        variantForm = new VariantFormData();
        // ✅ Inherit base product values for new variant
        variantForm.Weight = productForm.BaseWeight;
        variantForm.ShippingCost = productForm.BaseShippingCost;
        variantForm.HasFreeShipping = productForm.HasFreeShipping;
        isVariantModalOpen = true;
        StateHasChanged();
    }

    private void EditVariant(string variantKey)
    {
        if (productForm.Variants != null && productForm.Variants.TryGetValue(variantKey, out var variant))
        {
            isEditingVariant = true;
            editingVariantKey = variantKey;
            variantForm = new VariantFormData
            {
                Sku = variant.Sku,
                Size = variant.Size,
                Color = variant.Color,
                ColorHex = variant.ColorHex,
                Stock = variant.Stock,
                PriceAdjustment = variant.PriceAdjustment,
                Weight = variant.Weight ?? productForm.BaseWeight,
                Length = variant.Length,
                Width = variant.Width,
                Height = variant.Height,
                ShippingCost = variant.ShippingCost ?? productForm.BaseShippingCost,
                HasFreeShipping = variant.HasFreeShipping,
                ImageUrl = variant.ImageUrl,
                IsAvailable = variant.IsAvailable
            };
            isVariantModalOpen = true;
            StateHasChanged();
        }
    }

    private void CloseVariantModal()
    {
        isVariantModalOpen = false;
        isEditingVariant = false;
        editingVariantKey = null;
        variantForm = new VariantFormData();
        StateHasChanged();
    }

    private void HandleVariantImageSelection(ChangeEventArgs e)
    {
        var selectedImageUrl = e.Value?.ToString() ?? "";
        
        if (!string.IsNullOrEmpty(selectedImageUrl))
        {
            variantForm.ImageUrl = selectedImageUrl;
            
            MID_HelperFunctions.DebugMessage(
                $"Variant image selected from dropdown: {selectedImageUrl}",
                LogLevel.Debug
            );
        }
        
        StateHasChanged();
    }

    private void HandleSaveVariant()
    {
        try
        {
            // Generate variant key from size and color
            var variantKey = GenerateVariantKey(variantForm.Size, variantForm.Color);

            if (string.IsNullOrEmpty(variantKey))
            {
                ShowErrorNotification("Variant must have at least a size or color");
                return;
            }

            // Check if variant key already exists (unless editing)
            if (!isEditingVariant && productForm.Variants != null && productForm.Variants.ContainsKey(variantKey))
            {
                ShowErrorNotification($"Variant '{variantKey}' already exists");
                return;
            }

            // ✅ Create ProductVariant with inheritance from main product
            var variant = new ProductVariant
            {
                Sku = variantForm.Sku,
                Size = variantForm.Size,
                Color = variantForm.Color,
                ColorHex = variantForm.ColorHex,
                Stock = variantForm.Stock,
                PriceAdjustment = variantForm.PriceAdjustment,
                // ✅ Only set if different from base product (null = inherit)
                Weight = variantForm.Weight != productForm.BaseWeight ? variantForm.Weight : null,
                Length = variantForm.Length,
                Width = variantForm.Width,
                Height = variantForm.Height,
                ShippingCost = variantForm.ShippingCost != productForm.BaseShippingCost ? variantForm.ShippingCost : null,
                HasFreeShipping = variantForm.HasFreeShipping,
                ImageUrl = variantForm.ImageUrl,
                IsAvailable = variantForm.IsAvailable
            };

            // Initialize variants dictionary if null
            if (productForm.Variants == null)
            {
                productForm.Variants = new Dictionary<string, ProductVariant>();
            }

            // If editing, remove old key first (if key changed)
            if (isEditingVariant && editingVariantKey != null && editingVariantKey != variantKey)
            {
                productForm.Variants.Remove(editingVariantKey);
            }

            // Add or update variant
            productForm.Variants[variantKey] = variant;

            ShowSuccessNotification($"Variant '{variantKey}' {(isEditingVariant ? "updated" : "added")} successfully");
            CloseVariantModal();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving variant");
            ShowErrorNotification($"Error saving variant: {ex.Message}");
        }
    }

    private void DeleteVariant(string variantKey)
    {
        if (productForm.Variants != null && productForm.Variants.Remove(variantKey))
        {
            ShowSuccessNotification($"Variant '{variantKey}' removed");
            StateHasChanged();
        }
    }

    private string GenerateVariantKey(string? size, string? color)
    {
        if (string.IsNullOrEmpty(size) && string.IsNullOrEmpty(color))
            return string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(size)) parts.Add(size.Trim());
        if (!string.IsNullOrEmpty(color)) parts.Add(color.Trim());

        return string.Join("_", parts);
    }

    // ==================== IMAGE HANDLING ====================

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

    // ==================== PRODUCT ACTIONS ====================

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
            duplicateForm.Id = 0;
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
        isStockModalOpen = true;
        StateHasChanged();
    }

    private void CloseStockModal()
    {
        isStockModalOpen = false;
        selectedProductForStock = null;
        StateHasChanged();
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

    // ==================== BULK ACTIONS ====================

    private void HandleSelectionChanged(int productId, bool isSelected)
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

        productsToDelete = new List<int>(selectedProducts);
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

    // ==================== EXPORT ====================

    private async Task HandleExport()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            var exportData = filteredProducts.Select(p => new
            {
                ID = p.Id,
                SKU = p.Sku,
                Name = p.Name,
                Owner = p.IsOwnedByStore ? "Store" : p.PartnerName ?? "Partner",
                Category = p.Category,
                Brand = p.Brand,
                Price = p.Price,
                OriginalPrice = p.OriginalPrice,
                Discount = p.Discount,
                Stock = p.Stock,
                VariantCount = p.Variants?.Count ?? 0,
                Status = p.IsActive ? "Active" : "Inactive",
                Featured = p.IsFeatured ? "Yes" : "No",
                OnSale = p.IsOnSale ? "Yes" : "No",
                Rating = p.Rating,
                Reviews = p.ReviewCount,
                CreatedAt = p.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                UpdatedAt = p.UpdatedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""
            }).ToList();

            var csv = new StringBuilder();
            
            // ✅ FIXED: Explicit column names without analytics
            csv.AppendLine("ID,SKU,Name,Owner,Category,Brand,Price,Original Price,Discount %,Stock,Variants,Status,Featured,On Sale,Rating,Reviews,Created At,Updated At");
            
            foreach (var item in exportData)
            {
                // ✅ FIXED: Explicit string formatting to avoid ambiguous calls
                var line = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17}",
                    item.ID.ToString(),
                    EscapeCsv(item.SKU),
                    EscapeCsv(item.Name),
                    EscapeCsv(item.Owner),
                    EscapeCsv(item.Category),
                    EscapeCsv(item.Brand),
                    item.Price.ToString("F2"),
                    item.OriginalPrice?.ToString("F2") ?? "",
                    item.Discount.ToString(),
                    item.Stock.ToString(),
                    item.VariantCount.ToString(),
                    item.Status,
                    item.Featured,
                    item.OnSale,
                    item.Rating.ToString("F1"),
                    item.Reviews.ToString(),
                    item.CreatedAt,
                    item.UpdatedAt
                );
                
                csv.AppendLine(line);
            }

            var fileName = $"products_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            var csvBytes = Encoding.UTF8.GetBytes(csv.ToString());
            var base64 = Convert.ToBase64String(csvBytes);

            await JSRuntime.InvokeVoidAsync("downloadFile", fileName, base64, "text/csv");
            
            ShowSuccessNotification($"Exported {exportData.Count} products successfully!");
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Exported {exportData.Count} products to {fileName}",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Exporting products");
            ShowErrorNotification($"Export failed: {ex.Message}");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";
        
        value = value.Replace("\"", "\"\"");
        
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            return $"\"{value}\"";
        
        return $"\"{value}\"";
    }

    // ==================== PAGINATION ====================

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

    // ==================== NOTIFICATIONS ====================

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

    // ==================== MAPPING ====================

    private ProductFormData MapToFormData(ProductViewModel product)
    {
        var form = new ProductFormData
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            LongDescription = product.LongDescription,
            
            // Partnership
            IsOwnedByStore = product.IsOwnedByStore,
            PartnerIdString = product.PartnerId?.ToString(),
            
            // Pricing
            Price = product.Price,
            OriginalPrice = product.OriginalPrice,
            
            // Shipping
            BaseWeight = product.BaseWeight,
            BaseShippingCost = product.BaseShippingCost,
            HasFreeShipping = product.HasFreeShipping,
            
            // Inventory
            Sku = product.Sku,
            
            // Classification
            CategoryId = product.CategoryId,
            Brand = product.Brand,
            Tags = product.Tags?.ToList(),
            
            // Media
            ImageUrls = product.Images?.ToList(),
            VideoUrl = product.VideoUrl,
            
            // Variants
            Variants = product.Variants != null 
                ? new Dictionary<string, ProductVariant>(product.Variants) 
                : new Dictionary<string, ProductVariant>(),
            
            // Settings
            IsFeatured = product.IsFeatured,
            IsActive = product.IsActive
        };
    
        form.InitializeRawInputs();
   
        return form;
    }

    private CreateProductRequest MapToCreateRequest(ProductFormData form)
    {
        return new CreateProductRequest
        {
            Name = form.Name,
            Description = form.Description,
            LongDescription = form.LongDescription,
            
            // Partnership
            IsOwnedByStore = form.IsOwnedByStore,
            PartnerId = string.IsNullOrEmpty(form.PartnerIdString) 
                ? null 
                : Guid.Parse(form.PartnerIdString),
            
            // Pricing
            Price = form.Price,
            OriginalPrice = form.OriginalPrice,
            
            // Shipping
            BaseWeight = form.BaseWeight,
            BaseShippingCost = form.BaseShippingCost,
            HasFreeShipping = form.HasFreeShipping,
            
            // Inventory
            Sku = form.Sku,
            
            // Classification
            CategoryId = form.CategoryId,
            Brand = form.Brand,
            Tags = form.Tags,
            
            // Media
            ImageUrls = form.ImageUrls,
            VideoUrl = form.VideoUrl,
            
            // Variants
            Variants = form.Variants,
            
            // Settings
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
            
            // Partnership
            IsOwnedByStore = form.IsOwnedByStore,
            PartnerId = string.IsNullOrEmpty(form.PartnerIdString) 
                ? null 
                : Guid.Parse(form.PartnerIdString),
            
            // Pricing
            Price = form.Price,
            OriginalPrice = form.OriginalPrice,
            
            // Shipping
            BaseWeight = form.BaseWeight,
            BaseShippingCost = form.BaseShippingCost,
            HasFreeShipping = form.HasFreeShipping,
            
            // Classification
            CategoryId = form.CategoryId,
            Brand = form.Brand,
            Tags = form.Tags,
            
            // Media
            ImageUrls = form.ImageUrls,
            VideoUrl = form.VideoUrl,
            
            // Variants
            Variants = form.Variants,
            
            // Settings
            IsFeatured = form.IsFeatured,
            IsActive = form.IsActive
        };
    }

    private void ResetFormData(ProductFormData form)
    {
        form.Id = 0;
        form.Name = "";
        form.Description = "";
        form.LongDescription = "";
        form.IsOwnedByStore = true;
        form.PartnerIdString = null;
        form.Price = 0;
        form.OriginalPrice = null;
        form.BaseWeight = 1.0m;
        form.BaseShippingCost = 2000m;
        form.HasFreeShipping = false;
        form.Sku = "";
        form.CategoryId = "";
        form.Brand = "";
        form.Tags?.Clear();
        form.ImageUrls?.Clear();
        form.VideoUrl = null;
        form.Variants?.Clear();
        form.IsFeatured = false;
        form.IsActive = true;
    }

    // ==================== FORM DATA CLASSES ====================

    public class ProductFormData
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string LongDescription { get; set; } = "";
        
        // Partnership
        public bool IsOwnedByStore { get; set; } = true;
        public string? PartnerIdString { get; set; }
        
        // Pricing
        public decimal Price { get; set; }
        public decimal? OriginalPrice { get; set; }
        
        // Shipping
        public decimal BaseWeight { get; set; } = 1.0m;
        public decimal BaseShippingCost { get; set; } = 2000m;
        public bool HasFreeShipping { get; set; } = false;
        
        // Inventory
        public string Sku { get; set; } = "";
        
        // Classification
        public string CategoryId { get; set; } = "";
        public string Brand { get; set; } = "";
        public List<string>? Tags { get; set; }
        
        // Media
        public List<string>? ImageUrls { get; set; }
        public string? VideoUrl { get; set; }
        
        // Variants
        public Dictionary<string, ProductVariant>? Variants { get; set; }
        
        // Settings
        public bool IsFeatured { get; set; }
        public bool IsActive { get; set; } = true;
        
        private string _tagsRawInput = "";
        
        public string TagsInput
        {
            get => _tagsRawInput;
            set
            {
                _tagsRawInput = value ?? "";
                Tags = ParseCommaSeparated(_tagsRawInput);
            }
        }
        
        private static List<string> ParseCommaSeparated(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new List<string>();
                
            return input
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .ToList();
        }
        
        public void InitializeRawInputs()
        {
            _tagsRawInput = Tags != null && Tags.Any() 
                ? string.Join(", ", Tags) 
                : "";
        }
    }

    public class VariantFormData
    {
        public string Sku { get; set; } = "";
        public string? Size { get; set; }
        public string? Color { get; set; }
        public string? ColorHex { get; set; }
        public int Stock { get; set; }
        public decimal PriceAdjustment { get; set; } = 0m;
        public decimal? Weight { get; set; }
        public decimal? Length { get; set; }
        public decimal? Width { get; set; }
        public decimal? Height { get; set; }
        public decimal? ShippingCost { get; set; }
        public bool HasFreeShipping { get; set; } = false;
        public string? ImageUrl { get; set; }
        public bool IsAvailable { get; set; } = true;
    }

    // ==================== DISPOSAL ====================

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
}