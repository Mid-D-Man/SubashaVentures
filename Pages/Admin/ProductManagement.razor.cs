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
    [Inject] private IProductService            ProductService           { get; set; } = default!;
    [Inject] private IProductOfTheDayService    ProductOfTheDayService   { get; set; } = default!;
    [Inject] private ISupabaseDatabaseService   SupabaseDatabaseService  { get; set; } = default!;
    [Inject] private IFirestoreService          FirestoreService         { get; set; } = default!;
    [Inject] private IBrandService              BrandService             { get; set; } = default!;
    [Inject] private IPartnerService            PartnerService           { get; set; } = default!;
    [Inject] private ICategoryService           CategoryService          { get; set; } = default!;
    [Inject] private ILogger<ProductManagement> Logger                   { get; set; } = default!;
    [Inject] private NavigationManager          NavigationManager        { get; set; } = default!;
    [Inject] private IJSRuntime                 JSRuntime                { get; set; } = default!;

    // ── Object pools ──────────────────────────────────────────────────────
    private MID_ComponentObjectPool<List<ProductViewModel>>? _productListPool;
    private MID_ComponentObjectPool<ProductFormData>?        _formDataPool;

    // ── UI state ──────────────────────────────────────────────────────────
    private bool isLoading            = true;
    private bool isProductModalOpen   = false;
    private bool isVariantModalOpen   = false;
    private bool isStockModalOpen     = false;
    private bool isImageSelectorOpen  = false;
    private bool isEditMode           = false;
    private bool isEditingVariant     = false;
    private bool isSaving             = false;
    private bool showDeleteConfirmation = false;

    private string viewMode           = "grid";
    private string searchQuery        = string.Empty;
    private string selectedCategory   = string.Empty;
    private string selectedOwnership  = string.Empty;
    private string selectedStockStatus = string.Empty;
    private string selectedStatus     = string.Empty;
    private string sortBy             = "newest";

    private int currentPage           = 1;
    private int pageSize              = 24;
    private int lowStockThreshold     = 10;

    // ── Stats ─────────────────────────────────────────────────────────────
    private int totalProducts     = 0;
    private int activeProducts    = 0;
    private int partnerProducts   = 0;
    private int outOfStockProducts = 0;
    private int featuredProducts  = 0;

    // ── Product of the Day ────────────────────────────────────────────────
    private ProductViewModel? productOfTheDay   = null;
    private DateTime?         potdLastUpdate    = null;

    // ── Data ──────────────────────────────────────────────────────────────
    private List<ProductViewModel>  allProducts       = new();
    private List<ProductViewModel>  filteredProducts  = new();
    private List<ProductViewModel>  paginatedProducts = new();
    private List<CategoryViewModel> categories        = new();
    private List<BrandModel>        brands            = new();
    private List<PartnerModel>      partners          = new();
    private List<int>               selectedProducts  = new();

    private List<string> commonTags   = new();
    private List<string> commonSizes  = new();
    private List<string> commonColors = new();

    // ── Form state ────────────────────────────────────────────────────────
    private ProductFormData             productForm        = new();
    private VariantFormData             variantForm        = new();
    private string?                     editingVariantKey  = null;
    private Dictionary<string, string>  validationErrors   = new();

    // ── Stock management ──────────────────────────────────────────────────
    private ProductViewModel? selectedProductForStock = null;

    // ── Delete confirmation ───────────────────────────────────────────────
    private ProductViewModel? productToDelete    = null;
    private List<int>?        productsToDelete   = null;

    // ── Component references ──────────────────────────────────────────────
    private DynamicModal?        productModal;
    private DynamicModal?        variantModal;
    private DynamicModal?        stockModal;
    private ImageSelectorPopup?  imageSelectorPopup;
    private ConfirmationPopup?   deleteConfirmationPopup;
    private NotificationComponent? notificationComponent;

    private int totalPages => (int)Math.Ceiling(filteredProducts.Count / (double)pageSize);

    // ==================== LIFECYCLE ====================

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _productListPool = new MID_ComponentObjectPool<List<ProductViewModel>>(
                () => new List<ProductViewModel>(),
                list => list.Clear(),
                maxPoolSize: 10);

            _formDataPool = new MID_ComponentObjectPool<ProductFormData>(
                () => new ProductFormData(),
                form => ResetFormData(form),
                maxPoolSize: 5);

            await MID_HelperFunctions.DebugMessageAsync("ProductManagement initialized", LogLevel.Info);

            await Task.WhenAll(
                LoadProductsAsync(),
                LoadCategoriesAsync(),
                LoadBrandsAsync(),
                LoadPartnersAsync(),
                LoadProductOfTheDayAsync()
            );

            CalculateStats();

            commonTags   = ProductService.GetCommonTags();
            commonSizes  = ProductService.GetCommonSizes();
            commonColors = ProductService.GetCommonColors();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "ProductManagement.OnInitializedAsync");
            ShowErrorNotification("Failed to initialize product management");
            isLoading = false;
        }
    }

    // ==================== DATA LOADING ====================

    private async Task LoadProductsAsync()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            // GetProductsAsync returns ALL non-deleted products (including inactive)
            var products = await ProductService.GetProductsAsync(0, 1000);
            allProducts  = products ?? new List<ProductViewModel>();
            totalProducts = allProducts.Count;

            // Enrich partner names
            foreach (var product in allProducts.Where(p => !p.IsOwnedByStore && p.PartnerId.HasValue))
            {
                var partner = partners.FirstOrDefault(p => p.Id == product.PartnerId!.Value);
                if (partner != null)
                    product.PartnerName = partner.Name;
            }

            ApplyFiltersAndSort();

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {totalProducts} products", LogLevel.Info);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "LoadProductsAsync");
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
            categories = await CategoryService.GetAllCategoriesAsync();
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {categories.Count} categories", LogLevel.Info);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "LoadCategoriesAsync");
            ShowErrorNotification("Failed to load categories");
            categories = new List<CategoryViewModel>();
        }
    }

    private async Task LoadBrandsAsync()
    {
        try
        {
            brands = await BrandService.GetAllBrandsAsync();
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {brands.Count} brands", LogLevel.Info);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "LoadBrandsAsync");
            brands = new List<BrandModel>();
        }
    }

    private async Task LoadPartnersAsync()
    {
        try
        {
            partners = await PartnerService.GetActivePartnersAsync();
            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {partners.Count} partners", LogLevel.Info);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "LoadPartnersAsync");
            partners = new List<PartnerModel>();
        }
    }

    private async Task LoadProductOfTheDayAsync()
    {
        try
        {
            productOfTheDay = await ProductOfTheDayService.GetProductOfTheDayAsync();
            potdLastUpdate  = await ProductOfTheDayService.GetLastUpdateTimeAsync();

            if (productOfTheDay != null)
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ POTD: {productOfTheDay.Name}", LogLevel.Info);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "LoadProductOfTheDayAsync");
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
                ShowErrorNotification("Failed to set Product of the Day.");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "SetAsProductOfTheDay");
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
                ShowSuccessNotification("Product of the Day refreshed!");
                StateHasChanged();
            }
            else
            {
                ShowErrorNotification("Failed to refresh Product of the Day");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "RefreshProductOfTheDay");
            ShowErrorNotification("Error refreshing Product of the Day");
        }
    }

    // ==================== FILTERING & SORTING ====================

    private void ApplyFiltersAndSort()
    {
        using var pooled = _productListPool?.GetPooled();
        var temp = pooled?.Object ?? new List<ProductViewModel>();

        temp.AddRange(allProducts.Where(p =>
            (string.IsNullOrEmpty(searchQuery) ||
             p.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
             p.Description.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
             p.Sku.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(selectedCategory)    || p.CategoryId == selectedCategory) &&
            (string.IsNullOrEmpty(selectedOwnership)   || FilterByOwnership(p, selectedOwnership)) &&
            (string.IsNullOrEmpty(selectedStockStatus) || FilterByStockStatus(p, selectedStockStatus)) &&
            (string.IsNullOrEmpty(selectedStatus)      || FilterByStatus(p, selectedStatus))
        ));

        filteredProducts = sortBy switch
        {
            "newest"     => temp.OrderByDescending(x => x.CreatedAt).ToList(),
            "oldest"     => temp.OrderBy(x => x.CreatedAt).ToList(),
            "name-az"    => temp.OrderBy(x => x.Name).ToList(),
            "name-za"    => temp.OrderByDescending(x => x.Name).ToList(),
            "price-high" => temp.OrderByDescending(x => x.Price).ToList(),
            "price-low"  => temp.OrderBy(x => x.Price).ToList(),
            "stock-high" => temp.OrderByDescending(x => x.Stock).ToList(),
            "stock-low"  => temp.OrderBy(x => x.Stock).ToList(),
            _            => temp
        };

        currentPage = 1;
        UpdatePaginatedProducts();
    }

    private static bool FilterByOwnership(ProductViewModel p, string ownership) => ownership switch
    {
        "owned"   => p.IsOwnedByStore,
        "partner" => !p.IsOwnedByStore && p.PartnerId.HasValue,
        _         => true
    };

    private bool FilterByStockStatus(ProductViewModel p, string status) => status switch
    {
        "in-stock"     => p.Stock > lowStockThreshold,
        "low-stock"    => p.Stock > 0 && p.Stock <= lowStockThreshold,
        "out-of-stock" => p.Stock == 0,
        _              => true
    };

    private static bool FilterByStatus(ProductViewModel p, string status) => status switch
    {
        "active"   => p.IsActive,
        "inactive" => !p.IsActive,
        "featured" => p.IsFeatured,
        "on-sale"  => p.IsOnSale,
        _          => true
    };

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
        activeProducts     = allProducts.Count(p => p.IsActive);
        partnerProducts    = allProducts.Count(p => !p.IsOwnedByStore && p.PartnerId.HasValue);
        outOfStockProducts = allProducts.Count(p => p.Stock == 0);
        featuredProducts   = allProducts.Count(p => p.IsFeatured);
    }

    private void HandleSearch() => ApplyFiltersAndSort();

    private void HandleCategoryFilter(ChangeEventArgs e)
    {
        selectedCategory = e.Value?.ToString() ?? string.Empty;
        ApplyFiltersAndSort();
    }

    private void HandleOwnershipFilter(ChangeEventArgs e)
    {
        selectedOwnership = e.Value?.ToString() ?? string.Empty;
        ApplyFiltersAndSort();
    }

    private void HandleStockFilter(ChangeEventArgs e)
    {
        selectedStockStatus = e.Value?.ToString() ?? string.Empty;
        ApplyFiltersAndSort();
    }

    private void HandleStatusFilter(ChangeEventArgs e)
    {
        selectedStatus = e.Value?.ToString() ?? string.Empty;
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
            if (isOwned) productForm.PartnerIdString = null;
            StateHasChanged();
        }
    }

    private void GenerateSku()
    {
        productForm.Sku = ProductService.GenerateUniqueSku();
        StateHasChanged();
    }

    private void GenerateVariantSku()
    {
        if (string.IsNullOrEmpty(productForm.Sku))
        {
            ShowWarningNotification("Please generate the main product SKU first");
            return;
        }

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(variantForm.Size))  parts.Add(variantForm.Size);
        if (!string.IsNullOrEmpty(variantForm.Color)) parts.Add(variantForm.Color);

        var suffix = parts.Any()
            ? string.Join("-", parts).ToUpperInvariant()
            : Guid.NewGuid().ToString("N")[..4].ToUpper();

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
                var updateReq = MapToUpdateRequest(productForm);
                var success   = await ProductService.UpdateProductAsync(productForm.Id, updateReq);

                if (success)
                {
                    ShowSuccessNotification($"Product '{productForm.Name}' updated successfully!");
                }
                else
                {
                    ShowErrorNotification("Failed to update product");
                    return;
                }
            }
            else
            {
                var createReq = MapToCreateRequest(productForm);
                var result    = await ProductService.CreateProductAsync(createReq);

                if (result != null)
                {
                    ShowSuccessNotification($"Product '{productForm.Name}' created successfully!");
                }
                else
                {
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
            await MID_HelperFunctions.LogExceptionAsync(ex, "HandleSaveProduct");
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
            validationErrors["Name"] = "Product name is required";

        if (productForm.Price <= 0)
            validationErrors["Price"] = "Price must be greater than 0";

        if (string.IsNullOrWhiteSpace(productForm.Sku))
            validationErrors["Sku"] = "SKU is required";

        if (string.IsNullOrWhiteSpace(productForm.CategoryId))
            validationErrors["CategoryId"] = "Category is required";

        if (!productForm.IsOwnedByStore && string.IsNullOrWhiteSpace(productForm.PartnerIdString))
            validationErrors["PartnerId"] = "Partner is required for partner products";

        return !validationErrors.Any();
    }

    private int CalculateDiscount()
    {
        if (productForm.OriginalPrice.HasValue && productForm.OriginalPrice > productForm.Price && productForm.Price > 0)
            return (int)Math.Round((productForm.OriginalPrice.Value - productForm.Price) / productForm.OriginalPrice.Value * 100);
        return 0;
    }

    // ==================== VARIANT MODAL ====================

    private void OpenVariantModal()
    {
        isEditingVariant  = false;
        editingVariantKey = null;
        variantForm       = new VariantFormData
        {
            Weight       = productForm.BaseWeight,
            ShippingCost = productForm.BaseShippingCost,
            HasFreeShipping = productForm.HasFreeShipping
        };
        isVariantModalOpen = true;
        StateHasChanged();
    }

    private void EditVariant(string variantKey)
    {
        if (productForm.Variants == null || !productForm.Variants.TryGetValue(variantKey, out var v))
            return;

        isEditingVariant  = true;
        editingVariantKey = variantKey;
        variantForm = new VariantFormData
        {
            Sku             = v.Sku,
            Size            = v.Size,
            Color           = v.Color,
            ColorHex        = v.ColorHex,
            Stock           = v.Stock,
            PriceAdjustment = v.PriceAdjustment,
            Weight          = v.Weight          ?? productForm.BaseWeight,
            Length          = v.Length,
            Width           = v.Width,
            Height          = v.Height,
            ShippingCost    = v.ShippingCost     ?? productForm.BaseShippingCost,
            HasFreeShipping = v.HasFreeShipping,
            ImageUrl        = v.ImageUrl,
            IsAvailable     = v.IsAvailable
        };
        isVariantModalOpen = true;
        StateHasChanged();
    }

    private void CloseVariantModal()
    {
        isVariantModalOpen = false;
        isEditingVariant   = false;
        editingVariantKey  = null;
        variantForm        = new VariantFormData();
        StateHasChanged();
    }

    private void HandleVariantImageSelection(ChangeEventArgs e)
    {
        var url = e.Value?.ToString() ?? string.Empty;
        if (!string.IsNullOrEmpty(url))
        {
            variantForm.ImageUrl = url;
            MID_HelperFunctions.DebugMessage($"Variant image selected: {url}", LogLevel.Debug);
        }
        StateHasChanged();
    }

    private void HandleSaveVariant()
    {
        try
        {
            var variantKey = GenerateVariantKey(variantForm.Size, variantForm.Color);

            if (string.IsNullOrEmpty(variantKey))
            {
                ShowErrorNotification("Variant must have at least a size or colour");
                return;
            }

            productForm.Variants ??= new Dictionary<string, ProductVariant>();

            if (!isEditingVariant && productForm.Variants.ContainsKey(variantKey))
            {
                ShowErrorNotification($"Variant '{variantKey}' already exists");
                return;
            }

            var variant = new ProductVariant
            {
                Sku             = variantForm.Sku,
                Size            = variantForm.Size,
                Color           = variantForm.Color,
                ColorHex        = variantForm.ColorHex,
                Stock           = variantForm.Stock,
                PriceAdjustment = variantForm.PriceAdjustment,
                Weight          = variantForm.Weight       != productForm.BaseWeight       ? variantForm.Weight       : null,
                Length          = variantForm.Length,
                Width           = variantForm.Width,
                Height          = variantForm.Height,
                ShippingCost    = variantForm.ShippingCost != productForm.BaseShippingCost  ? variantForm.ShippingCost : null,
                HasFreeShipping = variantForm.HasFreeShipping,
                ImageUrl        = variantForm.ImageUrl,
                IsAvailable     = variantForm.IsAvailable
            };

            if (isEditingVariant && editingVariantKey != null && editingVariantKey != variantKey)
                productForm.Variants.Remove(editingVariantKey);

            productForm.Variants[variantKey] = variant;

            ShowSuccessNotification($"Variant '{variantKey}' {(isEditingVariant ? "updated" : "added")}");
            CloseVariantModal();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "HandleSaveVariant error");
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

    private static string GenerateVariantKey(string? size, string? color)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(size))  parts.Add(size!.Trim());
        if (!string.IsNullOrWhiteSpace(color)) parts.Add(color!.Trim());
        return parts.Any() ? string.Join("_", parts) : string.Empty;
    }

    // ==================== IMAGE HANDLING ====================

    private void OpenImageSelector()  { isImageSelectorOpen = true;  StateHasChanged(); }
    private void CloseImageSelector() { isImageSelectorOpen = false; StateHasChanged(); }

    private void HandleImagesSelected(List<string> urls)
    {
        productForm.ImageUrls = urls;
        CloseImageSelector();
        StateHasChanged();
    }

    private void RemoveImage(string url)
    {
        productForm.ImageUrls?.Remove(url);
        StateHasChanged();
    }

    // ==================== PRODUCT ACTIONS ====================

    private void HandleDeleteProduct(ProductViewModel product)
    {
        productToDelete     = product;
        productsToDelete    = null;
        showDeleteConfirmation = true;
        StateHasChanged();
    }

    private async Task ConfirmDeleteProduct()
    {
        if (productToDelete == null) return;
        try
        {
            var success = await ProductService.DeleteProductAsync(productToDelete.Id);
            if (success)
            {
                ShowSuccessNotification($"Product '{productToDelete.Name}' deleted");
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
            await MID_HelperFunctions.LogExceptionAsync(ex, "ConfirmDeleteProduct");
            ShowErrorNotification($"Error: {ex.Message}");
        }
        finally
        {
            showDeleteConfirmation = false;
            productToDelete        = null;
            StateHasChanged();
        }
    }

    private async Task HandleDuplicateProduct(ProductViewModel product)
    {
        try
        {
            var form      = MapToFormData(product);
            form.Id       = 0;
            form.Name     = $"{product.Name} (Copy)";
            form.Sku      = ProductService.GenerateUniqueSku();

            var result = await ProductService.CreateProductAsync(MapToCreateRequest(form));
            if (result != null)
            {
                ShowSuccessNotification($"Product duplicated as '{form.Name}'");
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
            await MID_HelperFunctions.LogExceptionAsync(ex, "HandleDuplicateProduct");
            ShowErrorNotification($"Error: {ex.Message}");
        }
    }

    private void HandlePreviewProduct(ProductViewModel product)
        => NavigationManager.NavigateTo($"/product/{product.Slug}");

    private void OpenStockModal(ProductViewModel product)
    {
        selectedProductForStock = product;
        isStockModalOpen        = true;
        StateHasChanged();
    }

    private void CloseStockModal()
    {
        isStockModalOpen        = false;
        selectedProductForStock = null;
        StateHasChanged();
    }

    private async Task HandleToggleActive(ProductViewModel product, bool isActive)
    {
        try
        {
            var success = await ProductService.UpdateProductAsync(product.Id,
                new UpdateProductRequest { IsActive = isActive });

            if (success)
            {
                ShowSuccessNotification($"Product {(isActive ? "activated" : "deactivated")}");
                await LoadProductsAsync();
                CalculateStats();
            }
            else ShowErrorNotification("Failed to update status");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "HandleToggleActive");
            ShowErrorNotification("Error updating product status");
        }
    }

    private async Task HandleToggleFeatured(ProductViewModel product, bool isFeatured)
    {
        try
        {
            var success = await ProductService.UpdateProductAsync(product.Id,
                new UpdateProductRequest { IsFeatured = isFeatured });

            if (success)
            {
                ShowSuccessNotification($"Product {(isFeatured ? "featured" : "unfeatured")}");
                await LoadProductsAsync();
                CalculateStats();
            }
            else ShowErrorNotification("Failed to update featured status");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "HandleToggleFeatured");
            ShowErrorNotification("Error updating featured status");
        }
    }

    // ==================== BULK ACTIONS ====================

    private void HandleSelectionChanged(int productId, bool isSelected)
    {
        if (isSelected) { if (!selectedProducts.Contains(productId)) selectedProducts.Add(productId); }
        else selectedProducts.Remove(productId);
        StateHasChanged();
    }

    private void HandleSelectAll(ChangeEventArgs e)
    {
        if (e.Value is bool isChecked)
        {
            selectedProducts = isChecked
                ? paginatedProducts.Select(p => p.Id).ToList()
                : new List<int>();
            StateHasChanged();
        }
    }

    private async Task HandleBulkActivate()
    {
        if (!selectedProducts.Any()) { ShowWarningNotification("No products selected"); return; }
        try
        {
            foreach (var id in selectedProducts)
                await ProductService.UpdateProductAsync(id, new UpdateProductRequest { IsActive = true });
            ShowSuccessNotification($"{selectedProducts.Count} products activated");
            selectedProducts.Clear();
            await LoadProductsAsync();
            CalculateStats();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "HandleBulkActivate");
            ShowErrorNotification("Error activating products");
        }
    }

    private async Task HandleBulkDeactivate()
    {
        if (!selectedProducts.Any()) { ShowWarningNotification("No products selected"); return; }
        try
        {
            foreach (var id in selectedProducts)
                await ProductService.UpdateProductAsync(id, new UpdateProductRequest { IsActive = false });
            ShowSuccessNotification($"{selectedProducts.Count} products deactivated");
            selectedProducts.Clear();
            await LoadProductsAsync();
            CalculateStats();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "HandleBulkDeactivate");
            ShowErrorNotification("Error deactivating products");
        }
    }

    private void HandleBulkDelete()
    {
        if (!selectedProducts.Any()) { ShowWarningNotification("No products selected"); return; }
        productsToDelete       = new List<int>(selectedProducts);
        productToDelete        = null;
        showDeleteConfirmation = true;
        StateHasChanged();
    }

    private async Task ConfirmBulkDelete()
    {
        if (productsToDelete == null || !productsToDelete.Any()) return;
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
            else ShowErrorNotification("Failed to delete products");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "ConfirmBulkDelete");
            ShowErrorNotification("Error deleting products");
        }
        finally
        {
            showDeleteConfirmation = false;
            productsToDelete       = null;
            StateHasChanged();
        }
    }

    // ==================== EXPORT ====================

    private async Task HandleExport()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            var csv = new StringBuilder();
            csv.AppendLine("ID,SKU,Name,Owner,Category,Brand,Price,Original Price,Discount %," +
                           "Stock,Variants,Status,Featured,On Sale,Rating,Reviews,Created At,Updated At");

            foreach (var p in filteredProducts)
            {
                var line = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17}",
                    p.Id,
                    EscapeCsv(p.Sku),
                    EscapeCsv(p.Name),
                    EscapeCsv(p.IsOwnedByStore ? "Store" : p.PartnerName ?? "Partner"),
                    EscapeCsv(p.Category),
                    EscapeCsv(p.Brand),
                    p.Price.ToString("F2"),
                    p.OriginalPrice?.ToString("F2") ?? string.Empty,
                    p.Discount,
                    p.Stock,
                    p.Variants?.Count ?? 0,
                    p.IsActive ? "Active" : "Inactive",
                    p.IsFeatured ? "Yes" : "No",
                    p.IsOnSale   ? "Yes" : "No",
                    p.Rating.ToString("F1"),
                    p.ReviewCount,
                    p.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    p.UpdatedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty);
                csv.AppendLine(line);
            }

            var fileName  = $"products_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            var bytes     = Encoding.UTF8.GetBytes(csv.ToString());
            var base64    = Convert.ToBase64String(bytes);

            await JSRuntime.InvokeVoidAsync("downloadFile", fileName, base64, "text/csv");
            ShowSuccessNotification($"Exported {filteredProducts.Count} products");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "HandleExport");
            ShowErrorNotification($"Export failed: {ex.Message}");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        value = value.Replace("\"", "\"\"");
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value}\""
            : $"\"{value}\"";
    }

    // ==================== PAGINATION ====================

    private static string GetStockClass(int stock)
    {
        if (stock <= 0)   return "out-of-stock";
        if (stock <= 10)  return "low";
        return "in-stock";
    }

    private void PreviousPage() { if (currentPage > 1)           { currentPage--; UpdatePaginatedProducts(); } }
    private void NextPage()     { if (currentPage < totalPages)  { currentPage++; UpdatePaginatedProducts(); } }

    private void GoToPage(int page)
    {
        if (page < 1 || page > totalPages) return;
        currentPage = page;
        UpdatePaginatedProducts();
    }

    // ==================== NOTIFICATIONS ====================
    // FIX: Use ShowNotification with NotificationType enum to match NotificationComponent API.

    private void ShowSuccessNotification(string message) =>
        notificationComponent?.ShowNotification(message, NotificationType.Success);

    private void ShowErrorNotification(string message) =>
        notificationComponent?.ShowNotification(message, NotificationType.Error);

    private void ShowWarningNotification(string message) =>
        notificationComponent?.ShowNotification(message, NotificationType.Warning);

    private void ShowInfoNotification(string message) =>
        notificationComponent?.ShowNotification(message, NotificationType.Info);

    // ==================== MAPPING ====================

    private ProductFormData MapToFormData(ProductViewModel p)
    {
        var form = new ProductFormData
        {
            Id               = p.Id,
            Name             = p.Name,
            Description      = p.Description,
            LongDescription  = p.LongDescription,
            IsOwnedByStore   = p.IsOwnedByStore,
            PartnerIdString  = p.PartnerId?.ToString(),
            Price            = p.Price,
            OriginalPrice    = p.OriginalPrice,
            BaseWeight       = p.BaseWeight,
            BaseShippingCost = p.BaseShippingCost,
            HasFreeShipping  = p.HasFreeShipping,
            Sku              = p.Sku,
            CategoryId       = p.CategoryId,
            Brand            = p.Brand,
            Tags             = p.Tags?.ToList(),
            ImageUrls        = p.Images?.ToList(),
            VideoUrl         = p.VideoUrl,
            Variants         = p.Variants != null
                ? new Dictionary<string, ProductVariant>(p.Variants)
                : new Dictionary<string, ProductVariant>(),
            IsFeatured       = p.IsFeatured,
            IsActive         = p.IsActive
        };
        form.InitializeRawInputs();
        return form;
    }

    private static CreateProductRequest MapToCreateRequest(ProductFormData f) => new()
    {
        Name             = f.Name,
        Description      = f.Description,
        LongDescription  = f.LongDescription,
        IsOwnedByStore   = f.IsOwnedByStore,
        PartnerId        = string.IsNullOrEmpty(f.PartnerIdString) ? null : Guid.Parse(f.PartnerIdString),
        Price            = f.Price,
        OriginalPrice    = f.OriginalPrice,
        BaseWeight       = f.BaseWeight,
        BaseShippingCost = f.BaseShippingCost,
        HasFreeShipping  = f.HasFreeShipping,
        Sku              = f.Sku,
        CategoryId       = f.CategoryId,
        Brand            = f.Brand,
        Tags             = f.Tags,
        ImageUrls        = f.ImageUrls,
        VideoUrl         = f.VideoUrl,
        Variants         = f.Variants,
        IsFeatured       = f.IsFeatured
    };

    private static UpdateProductRequest MapToUpdateRequest(ProductFormData f) => new()
    {
        Name             = f.Name,
        Description      = f.Description,
        LongDescription  = f.LongDescription,
        IsOwnedByStore   = f.IsOwnedByStore,
        PartnerId        = string.IsNullOrEmpty(f.PartnerIdString) ? null : Guid.Parse(f.PartnerIdString),
        Price            = f.Price,
        OriginalPrice    = f.OriginalPrice,
        BaseWeight       = f.BaseWeight,
        BaseShippingCost = f.BaseShippingCost,
        HasFreeShipping  = f.HasFreeShipping,
        CategoryId       = f.CategoryId,
        Brand            = f.Brand,
        Tags             = f.Tags,
        ImageUrls        = f.ImageUrls,
        VideoUrl         = f.VideoUrl,
        Variants         = f.Variants,
        IsFeatured       = f.IsFeatured,
        IsActive         = f.IsActive
    };

    private static void ResetFormData(ProductFormData f)
    {
        f.Id = 0; f.Name = string.Empty; f.Description = string.Empty;
        f.LongDescription = string.Empty; f.IsOwnedByStore = true;
        f.PartnerIdString = null; f.Price = 0; f.OriginalPrice = null;
        f.BaseWeight = 1.0m; f.BaseShippingCost = 2000m; f.HasFreeShipping = false;
        f.Sku = string.Empty; f.CategoryId = string.Empty; f.Brand = string.Empty;
        f.Tags?.Clear(); f.ImageUrls?.Clear(); f.VideoUrl = null;
        f.Variants?.Clear(); f.IsFeatured = false; f.IsActive = true;
    }

    // ==================== FORM DATA CLASSES ====================

    public class ProductFormData
    {
        public int     Id              { get; set; }
        public string  Name            { get; set; } = string.Empty;
        public string  Description     { get; set; } = string.Empty;
        public string  LongDescription { get; set; } = string.Empty;
        public bool    IsOwnedByStore  { get; set; } = true;
        public string? PartnerIdString { get; set; }
        public decimal Price           { get; set; }
        public decimal? OriginalPrice  { get; set; }
        public decimal BaseWeight      { get; set; } = 1.0m;
        public decimal BaseShippingCost { get; set; } = 2000m;
        public bool    HasFreeShipping { get; set; } = false;
        public string  Sku             { get; set; } = string.Empty;
        public string  CategoryId      { get; set; } = string.Empty;
        public string  Brand           { get; set; } = string.Empty;
        public List<string>?                        Tags      { get; set; }
        public List<string>?                        ImageUrls { get; set; }
        public string?                              VideoUrl  { get; set; }
        public Dictionary<string, ProductVariant>?  Variants  { get; set; }
        public bool    IsFeatured      { get; set; }
        public bool    IsActive        { get; set; } = true;

        private string _tagsRaw = string.Empty;

        public string TagsInput
        {
            get => _tagsRaw;
            set
            {
                _tagsRaw = value ?? string.Empty;
                Tags = ParseCommaSeparated(_tagsRaw);
            }
        }

        private static List<string> ParseCommaSeparated(string input) =>
            string.IsNullOrWhiteSpace(input)
                ? new List<string>()
                : input.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => s.Trim())
                       .Where(s => !string.IsNullOrEmpty(s))
                       .Distinct()
                       .ToList();

        public void InitializeRawInputs() =>
            _tagsRaw = Tags != null && Tags.Any() ? string.Join(", ", Tags) : string.Empty;
    }

    public class VariantFormData
    {
        public string   Sku             { get; set; } = string.Empty;
        public string?  Size            { get; set; }
        public string?  Color           { get; set; }
        public string?  ColorHex        { get; set; }
        public int      Stock           { get; set; }
        public decimal  PriceAdjustment { get; set; } = 0m;
        public decimal? Weight          { get; set; }
        public decimal? Length          { get; set; }
        public decimal? Width           { get; set; }
        public decimal? Height          { get; set; }
        public decimal? ShippingCost    { get; set; }
        public bool     HasFreeShipping { get; set; } = false;
        public string?  ImageUrl        { get; set; }
        public bool     IsAvailable     { get; set; } = true;
    }

    // ==================== DISPOSAL ====================

    public async ValueTask DisposeAsync()
    {
        try
        {
            _productListPool?.Dispose();
            _formDataPool?.Dispose();
            await MID_HelperFunctions.DebugMessageAsync("ProductManagement disposed", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "ProductManagement dispose error");
        }
    }
}
