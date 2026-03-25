using Microsoft.AspNetCore.Components;
using SubashaVentures.Services.Categories;
using SubashaVentures.Services.VisualElements;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Components.Shared.Notifications;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Domain.Product;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Admin;

public partial class CategoryManagement : ComponentBase
{
    [Inject] private ICategoryService CategoryService { get; set; } = default!;
    [Inject] private IVisualElementsService VisualElements { get; set; } = default!;
    [Inject] private ILogger<CategoryManagement> Logger { get; set; } = default!;

    // ==================== STATE ====================

    private bool isLoading = true;
    private bool isSaving = false;
    private bool isDeleting = false;

    // Category modal
    private bool isCategoryModalOpen = false;
    private bool isEditMode = false;
    private bool showCategoryIconPicker = false;
    private string? selectedCategoryIconSvg = null;

    // Subcategory modal
    private bool isSubCategoryModalOpen = false;
    private bool isEditSubMode = false;
    private bool showSubIconPicker = false;
    private string? selectedSubIconSvg = null;
    private CategoryViewModel? activeParentCategory = null;

    // Confirmation
    private bool isConfirmationOpen = false;
    private string confirmTitle = "";
    private string confirmMessage = "";
    private Func<Task>? pendingDeleteAction = null;

    // Expand/collapse
    private HashSet<string> expandedCategories = new();

    // Data
    private List<CategoryViewModel> categories = new();

    // Forms
    private CategoryFormData categoryForm = new();
    private SubCategoryFormData subCategoryForm = new();
    private string? editingCategoryId = null;
    private string? editingSubCategoryId = null;

    // Validation
    private Dictionary<string, string> validationErrors = new();

    // SVG caches
    private Dictionary<string, string> categorySvgCache = new();
    private Dictionary<string, string> subCategorySvgCache = new();
    private Dictionary<SvgType, string> iconSvgCache = new();

    private readonly List<SvgType> availableIcons = new()
    {
        SvgType.AllProducts,
        SvgType.Beddings,
        SvgType.ChildrenClothes,
        SvgType.Dress,
        SvgType.Tuxido,
        SvgType.ShopNow,
        SvgType.Cart,
        SvgType.Wishlist,
        SvgType.Heart,
        SvgType.Star,
        SvgType.ThumbsUp,
        SvgType.Flame
    };

    // Component refs
    private DynamicModal? categoryModal;
    private DynamicModal? subCategoryModal;
    private ConfirmationPopup? confirmationPopup;
    private NotificationComponent? notificationComponent;

    // ==================== LIFECYCLE ====================

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await PreloadIconSvgsAsync();
            await LoadCategoriesAsync();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "CategoryManagement init");
            ShowError("Failed to initialize");
        }
    }

    // ==================== DATA LOADING ====================

    private async Task LoadCategoriesAsync()
    {
        isLoading = true;
        StateHasChanged();

        try
        {
            categories = await CategoryService.GetCategoriesWithSubcategoriesAsync();

            categorySvgCache.Clear();
            subCategorySvgCache.Clear();

            foreach (var cat in categories)
            {
                if (cat.IconSvgType != SvgType.None)
                {
                    categorySvgCache[cat.Id] = await VisualElements.GetSvgWithColorAsync(
                        cat.IconSvgType, 24, 24, "var(--primary-color)");
                }

                foreach (var sub in cat.SubCategories)
                {
                    if (sub.IconSvgType != SvgType.None)
                    {
                        subCategorySvgCache[sub.Id] = await VisualElements.GetSvgWithColorAsync(
                            sub.IconSvgType, 20, 20, "var(--primary-color)");
                    }
                }
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"✓ Loaded {categories.Count} categories", LogLevel.Info);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "LoadCategoriesAsync");
            ShowError("Failed to load categories");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task PreloadIconSvgsAsync()
    {
        foreach (var icon in availableIcons)
        {
            iconSvgCache[icon] = await VisualElements.GetSvgWithColorAsync(
                icon, 28, 28, "var(--text-secondary)");
        }
    }

    // ==================== EXPAND / COLLAPSE ====================

    private void ToggleExpand(string categoryId)
    {
        if (expandedCategories.Contains(categoryId))
            expandedCategories.Remove(categoryId);
        else
            expandedCategories.Add(categoryId);

        StateHasChanged();
    }

    // ==================== CATEGORY MODAL ====================

    private async Task OpenAddCategoryModal()
    {
        isEditMode = false;
        editingCategoryId = null;
        categoryForm = new CategoryFormData();
        validationErrors.Clear();
        showCategoryIconPicker = false;
        selectedCategoryIconSvg = null;
        isCategoryModalOpen = true;
        StateHasChanged();
    }

    private async Task OpenEditCategoryModal(CategoryViewModel category)
    {
        isEditMode = true;
        editingCategoryId = category.Id;
        categoryForm = new CategoryFormData
        {
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            IconSvgType = category.IconSvgType,
            DisplayOrder = category.DisplayOrder,
            IsActive = category.IsActive
        };

        validationErrors.Clear();
        showCategoryIconPicker = false;

        if (category.IconSvgType != SvgType.None)
        {
            selectedCategoryIconSvg = await VisualElements.GetSvgWithColorAsync(
                category.IconSvgType, 32, 32, "var(--primary-color)");
        }
        else
        {
            selectedCategoryIconSvg = null;
        }

        isCategoryModalOpen = true;
        StateHasChanged();
    }

    private void CloseCategoryModal()
    {
        isCategoryModalOpen = false;
        categoryForm = new CategoryFormData();
        validationErrors.Clear();
        showCategoryIconPicker = false;
        selectedCategoryIconSvg = null;
        StateHasChanged();
    }

    private void ToggleCategoryIconPicker()
    {
        showCategoryIconPicker = !showCategoryIconPicker;
        StateHasChanged();
    }

    private async Task SelectCategoryIcon(SvgType icon)
    {
        categoryForm.IconSvgType = icon;
        showCategoryIconPicker = false;
        selectedCategoryIconSvg = await VisualElements.GetSvgWithColorAsync(
            icon, 32, 32, "var(--primary-color)");
        StateHasChanged();
    }

    private async Task SaveCategory()
    {
        validationErrors.Clear();

        if (string.IsNullOrWhiteSpace(categoryForm.Name))
        {
            validationErrors["Name"] = "Category name is required";
            StateHasChanged();
            return;
        }

        isSaving = true;
        StateHasChanged();

        try
        {
            if (isEditMode && editingCategoryId != null)
            {
                var request = new UpdateCategoryRequest
                {
                    Name = categoryForm.Name,
                    Description = categoryForm.Description,
                    IconSvgType = categoryForm.IconSvgType,
                    DisplayOrder = categoryForm.DisplayOrder,
                    IsActive = categoryForm.IsActive
                };

                var success = await CategoryService.UpdateCategoryAsync(editingCategoryId, request);
                if (success)
                {
                    ShowSuccess($"Category '{categoryForm.Name}' updated");
                    CloseCategoryModal();
                    await LoadCategoriesAsync();
                }
                else
                {
                    ShowError("Failed to update category");
                }
            }
            else
            {
                var request = new CreateCategoryRequest
                {
                    Name = categoryForm.Name,
                    Description = categoryForm.Description,
                    IconSvgType = categoryForm.IconSvgType,
                    DisplayOrder = categoryForm.DisplayOrder
                };

                var id = await CategoryService.CreateCategoryAsync(request);
                if (!string.IsNullOrEmpty(id))
                {
                    ShowSuccess($"Category '{categoryForm.Name}' created");
                    CloseCategoryModal();
                    await LoadCategoriesAsync();
                }
                else
                {
                    ShowError("Failed to create category");
                }
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "SaveCategory");
            ShowError("An error occurred");
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    // ==================== SUBCATEGORY MODAL ====================

    private async Task OpenAddSubCategoryModal(CategoryViewModel parent)
    {
        isEditSubMode = false;
        editingSubCategoryId = null;
        activeParentCategory = parent;
        subCategoryForm = new SubCategoryFormData
        {
            CategoryId = parent.Id,
            // Auto-default if parent has no subcategories yet
            IsDefault = !parent.SubCategories.Any()
        };

        validationErrors.Clear();
        showSubIconPicker = false;
        selectedSubIconSvg = null;

        // Auto-expand the parent
        expandedCategories.Add(parent.Id);

        isSubCategoryModalOpen = true;
        StateHasChanged();
    }

    private async Task OpenEditSubCategoryModal(SubCategoryViewModel sub, CategoryViewModel parent)
    {
        isEditSubMode = true;
        editingSubCategoryId = sub.Id;
        activeParentCategory = parent;
        subCategoryForm = new SubCategoryFormData
        {
            CategoryId = sub.CategoryId,
            Name = sub.Name,
            Slug = sub.Slug,
            Description = sub.Description,
            IconSvgType = sub.IconSvgType,
            IsDefault = sub.IsDefault,
            DisplayOrder = sub.DisplayOrder,
            IsActive = sub.IsActive
        };

        validationErrors.Clear();
        showSubIconPicker = false;

        if (sub.IconSvgType != SvgType.None)
        {
            selectedSubIconSvg = await VisualElements.GetSvgWithColorAsync(
                sub.IconSvgType, 32, 32, "var(--primary-color)");
        }
        else
        {
            selectedSubIconSvg = null;
        }

        isSubCategoryModalOpen = true;
        StateHasChanged();
    }

    private void CloseSubCategoryModal()
    {
        isSubCategoryModalOpen = false;
        subCategoryForm = new SubCategoryFormData();
        validationErrors.Clear();
        showSubIconPicker = false;
        selectedSubIconSvg = null;
        activeParentCategory = null;
        StateHasChanged();
    }

    private void ToggleSubIconPicker()
    {
        showSubIconPicker = !showSubIconPicker;
        StateHasChanged();
    }

    private async Task SelectSubIcon(SvgType icon)
    {
        subCategoryForm.IconSvgType = icon;
        showSubIconPicker = false;
        selectedSubIconSvg = await VisualElements.GetSvgWithColorAsync(
            icon, 32, 32, "var(--primary-color)");
        StateHasChanged();
    }

    private async Task SaveSubCategory()
    {
        validationErrors.Clear();

        if (string.IsNullOrWhiteSpace(subCategoryForm.Name))
        {
            validationErrors["SubName"] = "Subcategory name is required";
            StateHasChanged();
            return;
        }

        isSaving = true;
        StateHasChanged();

        try
        {
            if (isEditSubMode && editingSubCategoryId != null)
            {
                var request = new UpdateSubCategoryRequest
                {
                    Name = subCategoryForm.Name,
                    Description = subCategoryForm.Description,
                    IconSvgType = subCategoryForm.IconSvgType,
                    IsDefault = subCategoryForm.IsDefault,
                    DisplayOrder = subCategoryForm.DisplayOrder,
                    IsActive = subCategoryForm.IsActive
                };

                var success = await CategoryService.UpdateSubCategoryAsync(editingSubCategoryId, request);
                if (success)
                {
                    ShowSuccess($"Subcategory '{subCategoryForm.Name}' updated");
                    CloseSubCategoryModal();
                    await LoadCategoriesAsync();
                }
                else
                {
                    ShowError("Failed to update subcategory");
                }
            }
            else
            {
                var request = new CreateSubCategoryRequest
                {
                    CategoryId = subCategoryForm.CategoryId,
                    Name = subCategoryForm.Name,
                    Description = subCategoryForm.Description,
                    IconSvgType = subCategoryForm.IconSvgType,
                    IsDefault = subCategoryForm.IsDefault,
                    DisplayOrder = subCategoryForm.DisplayOrder
                };

                var id = await CategoryService.CreateSubCategoryAsync(request);
                if (!string.IsNullOrEmpty(id))
                {
                    ShowSuccess($"Subcategory '{subCategoryForm.Name}' created");
                    CloseSubCategoryModal();
                    await LoadCategoriesAsync();
                }
                else
                {
                    ShowError("Failed to create subcategory");
                }
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "SaveSubCategory");
            ShowError("An error occurred");
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    // ==================== DELETE ====================

    private void HandleDeleteCategory(CategoryViewModel category)
    {
        if (category.ProductCount > 0)
        {
            ShowWarning("Cannot delete a category that has products");
            return;
        }

        confirmTitle = "Delete Category?";
        confirmMessage = $"Delete '{category.Name}' and all its subcategories?";
        pendingDeleteAction = async () =>
        {
            var success = await CategoryService.DeleteCategoryAsync(category.Id);
            if (success)
            {
                ShowSuccess($"Category '{category.Name}' deleted");
                await LoadCategoriesAsync();
            }
            else
            {
                ShowError("Failed to delete category");
            }
        };

        isConfirmationOpen = true;
        StateHasChanged();
    }

    private void HandleDeleteSubCategory(SubCategoryViewModel sub)
    {
        if (sub.ProductCount > 0)
        {
            ShowWarning("Cannot delete a subcategory that has products");
            return;
        }

        if (sub.IsDefault)
        {
            ShowWarning("Cannot delete the default subcategory — set another as default first");
            return;
        }

        confirmTitle = "Delete Subcategory?";
        confirmMessage = $"Delete subcategory '{sub.Name}'?";
        pendingDeleteAction = async () =>
        {
            var success = await CategoryService.DeleteSubCategoryAsync(sub.Id);
            if (success)
            {
                ShowSuccess($"Subcategory '{sub.Name}' deleted");
                await LoadCategoriesAsync();
            }
            else
            {
                ShowError("Failed to delete subcategory");
            }
        };

        isConfirmationOpen = true;
        StateHasChanged();
    }

    private async Task ConfirmDelete()
    {
        isDeleting = true;
        StateHasChanged();

        try
        {
            if (pendingDeleteAction != null)
                await pendingDeleteAction();
        }
        finally
        {
            isDeleting = false;
            isConfirmationOpen = false;
            pendingDeleteAction = null;
            StateHasChanged();
        }
    }

    private void CancelDelete()
    {
        isConfirmationOpen = false;
        pendingDeleteAction = null;
        StateHasChanged();
    }

    // ==================== SET DEFAULT ====================

    private async Task HandleSetDefault(SubCategoryViewModel sub, string categoryId)
    {
        try
        {
            var success = await CategoryService.SetDefaultSubCategoryAsync(sub.Id, categoryId);
            if (success)
            {
                ShowSuccess($"'{sub.Name}' is now the default subcategory");
                await LoadCategoriesAsync();
            }
            else
            {
                ShowError("Failed to set default");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "HandleSetDefault");
            ShowError("An error occurred");
        }
    }

    // ==================== HELPERS ====================

    private string GetSubDeleteTooltip(SubCategoryViewModel sub)
    {
        if (sub.ProductCount > 0) return "Has products";
        if (sub.IsDefault) return "Cannot delete default subcategory";
        return "Delete";
    }

    private void ShowSuccess(string msg) =>
        notificationComponent?.ShowNotification(msg, NotificationType.Success);

    private void ShowError(string msg) =>
        notificationComponent?.ShowNotification(msg, NotificationType.Error);

    private void ShowWarning(string msg) =>
        notificationComponent?.ShowNotification(msg, NotificationType.Warning);

    // ==================== FORM DATA ====================

    public class CategoryFormData
    {
        public string Name { get; set; } = "";
        public string Slug { get; set; } = "";
        public string? Description { get; set; }
        public SvgType IconSvgType { get; set; } = SvgType.None;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class SubCategoryFormData
    {
        public string CategoryId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Slug { get; set; } = "";
        public string? Description { get; set; }
        public SvgType IconSvgType { get; set; } = SvgType.None;
        public bool IsDefault { get; set; } = false;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
