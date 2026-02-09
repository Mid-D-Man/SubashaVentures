// Pages/Admin/CategoryManagement.razor.cs
using Microsoft.AspNetCore.Components;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Services.VisualElements;
using SubashaVentures.Services.Categories;
using SubashaVentures.Models.Firebase;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Components.Shared.Notifications;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Domain.Product;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Admin;

public partial class CategoryManagement : ComponentBase
{
    [Inject] private IFirestoreService FirestoreService { get; set; } = default!;
    [Inject] private IVisualElementsService VisualElements { get; set; } = default!;
    [Inject] private ICategoryService CategoryService { get; set; } = default!;
    [Inject] private ILogger<CategoryManagement> Logger { get; set; } = default!;

    private bool isLoading = true;
    private bool isCategoryModalOpen = false;
    private bool isEditMode = false;
    private bool isSaving = false;
    private bool isConfirmationOpen = false;
    private bool isDeleting = false;
    private bool showIconPicker = false;

    private List<CategoryViewModel> categories = new();
    private CategoryFormData editingCategory = new();
    private CategoryModel? categoryToDelete = null;
    private Dictionary<string, string> validationErrors = new();
    
    // Cache for pre-rendered SVGs
    private Dictionary<string, string> categorySvgCache = new();
    private Dictionary<SvgType, string> iconSvgCache = new();
    private string? selectedIconSvg = null;
    
    // Available category icons (curated list)
    private readonly List<SvgType> availableCategoryIcons = new()
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

    private DynamicModal? categoryModal;
    private ConfirmationPopup? confirmationPopup;
    private NotificationComponent? notificationComponent;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Pre-load all icon SVGs for picker
            await PreloadIconSvgs();
            await LoadCategoriesAsync();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "CategoryManagement initialization");
            ShowError("Failed to initialize category management");
        }
    }

    private async Task PreloadIconSvgs()
    {
        try
        {
            foreach (var iconType in availableCategoryIcons)
            {
                var svg = await VisualElements.GetSvgWithColorAsync(
                    iconType, 
                    28, 
                    28, 
                    "var(--text-secondary)");
                iconSvgCache[iconType] = svg;
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Preloading icon SVGs");
        }
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            categories = await CategoryService.GetAllCategoriesAsync();
            
            // Pre-render category SVGs for table
            categorySvgCache.Clear();
            foreach (var category in categories)
            {
                if (category.IconSvgType != SvgType.None)
                {
                    var svg = await VisualElements.GetSvgWithColorAsync(
                        category.IconSvgType, 
                        24, 
                        24, 
                        "var(--primary-color)");
                    categorySvgCache[category.Id] = svg;
                }
            }
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded {categories.Count} categories",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading categories");
            ShowError("Failed to load categories");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task OpenAddCategoryModal()
    {
        isEditMode = false;
        editingCategory = new CategoryFormData
        {
            Id = Guid.NewGuid().ToString(),
            IsActive = true,
            DisplayOrder = categories.Count,
            CreatedAt = DateTime.UtcNow,
            IconSvgType = SvgType.AllProducts // Default icon
        };
        validationErrors.Clear();
        showIconPicker = false;
        
        // Pre-render selected icon
        selectedIconSvg = await VisualElements.GetSvgWithColorAsync(
            editingCategory.IconSvgType, 
            32, 
            32, 
            "var(--primary-color)");
        
        isCategoryModalOpen = true;
        StateHasChanged();
    }

    private async Task OpenEditCategoryModal(CategoryViewModel category)
    {
        isEditMode = true;
        editingCategory = new CategoryFormData
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            IconSvgType = category.IconSvgType,
            ImageUrl = category.ImageUrl,
            ParentId = category.ParentId,
            DisplayOrder = category.DisplayOrder,
            IsActive = category.IsActive,
            ProductCount = category.ProductCount,
            CreatedAt = category.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };
        validationErrors.Clear();
        showIconPicker = false;
        
        // Pre-render selected icon
        if (editingCategory.IconSvgType != SvgType.None)
        {
            selectedIconSvg = await VisualElements.GetSvgWithColorAsync(
                editingCategory.IconSvgType, 
                32, 
                32, 
                "var(--primary-color)");
        }
        
        isCategoryModalOpen = true;
        StateHasChanged();
    }

    private void CloseCategoryModal()
    {
        isCategoryModalOpen = false;
        editingCategory = new();
        validationErrors.Clear();
        showIconPicker = false;
        selectedIconSvg = null;
        StateHasChanged();
    }

    private void ToggleIconPicker()
    {
        showIconPicker = !showIconPicker;
        StateHasChanged();
    }

    private async Task SelectIcon(SvgType iconType)
    {
        editingCategory.IconSvgType = iconType;
        showIconPicker = false;
        
        // Update selected icon preview
        selectedIconSvg = await VisualElements.GetSvgWithColorAsync(
            iconType, 
            32, 
            32, 
            "var(--primary-color)");
        
        StateHasChanged();
    }

    private async Task SaveCategory()
    {
        try
        {
            if (!ValidateCategory())
                return;

            isSaving = true;
            StateHasChanged();

            var request = isEditMode 
                ? new UpdateCategoryRequest
                {
                    Name = editingCategory.Name,
                    Description = editingCategory.Description,
                    ImageUrl = editingCategory.ImageUrl,
                    IconSvgType = editingCategory.IconSvgType,
                    DisplayOrder = editingCategory.DisplayOrder,
                    IsActive = editingCategory.IsActive
                }
                : null;

            bool success;
            if (isEditMode)
            {
                success = await CategoryService.UpdateCategoryAsync(editingCategory.Id, request!);
            }
            else
            {
                var createRequest = new CreateCategoryRequest
                {
                    Name = editingCategory.Name,
                    Description = editingCategory.Description,
                    ImageUrl = editingCategory.ImageUrl,
                    IconSvgType = editingCategory.IconSvgType,
                    ParentId = editingCategory.ParentId,
                    DisplayOrder = editingCategory.DisplayOrder
                };
                
                var id = await CategoryService.CreateCategoryAsync(createRequest);
                success = !string.IsNullOrEmpty(id);
            }

            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Category {(isEditMode ? "updated" : "created")}: {editingCategory.Name}",
                    LogLevel.Info
                );

                ShowSuccess($"Category '{editingCategory.Name}' {(isEditMode ? "updated" : "created")} successfully");
                await LoadCategoriesAsync();
                CloseCategoryModal();
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Failed to save category",
                    LogLevel.Error
                );
                ShowError("Failed to save category");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Saving category");
            ShowError("An error occurred while saving");
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    private bool ValidateCategory()
    {
        validationErrors.Clear();

        if (string.IsNullOrWhiteSpace(editingCategory.Name))
        {
            validationErrors["Name"] = "Category name is required";
        }

        if (validationErrors.Any())
        {
            StateHasChanged();
            return false;
        }

        return true;
    }

    private void HandleDeleteCategory(CategoryViewModel category)
    {
        if (category.ProductCount > 0)
        {
            MID_HelperFunctions.DebugMessage(
                "Cannot delete category with products",
                LogLevel.Warning
            );
            ShowWarning("Cannot delete category with products");
            return;
        }

        categoryToDelete = new CategoryModel
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            ImageUrl = category.ImageUrl,
            IconSvgType = category.IconSvgType,
            ParentId = category.ParentId,
            ProductCount = category.ProductCount,
            DisplayOrder = category.DisplayOrder,
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };
        
        isConfirmationOpen = true;
        StateHasChanged();
    }

    private async Task ConfirmDeleteCategory()
    {
        if (categoryToDelete == null)
            return;

        try
        {
            isDeleting = true;
            StateHasChanged();

            var success = await CategoryService.DeleteCategoryAsync(categoryToDelete.Id);

            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Category deleted: {categoryToDelete.Name}",
                    LogLevel.Info
                );

                ShowSuccess($"Category '{categoryToDelete.Name}' deleted successfully");
                await LoadCategoriesAsync();
            }
            else
            {
                ShowError("Failed to delete category");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Deleting category");
            ShowError("An error occurred while deleting");
        }
        finally
        {
            isDeleting = false;
            isConfirmationOpen = false;
            categoryToDelete = null;
            StateHasChanged();
        }
    }

    private void CancelDeleteCategory()
    {
        isConfirmationOpen = false;
        categoryToDelete = null;
        StateHasChanged();
    }

    private void ShowSuccess(string message)
    {
        notificationComponent?.ShowNotification(message, NotificationType.Success);
    }

    private void ShowError(string message)
    {
        notificationComponent?.ShowNotification(message, NotificationType.Error);
    }

    private void ShowWarning(string message)
    {
        notificationComponent?.ShowNotification(message, NotificationType.Warning);
    }

    private void ShowInfo(string message)
    {
        notificationComponent?.ShowNotification(message, NotificationType.Info);
    }

    // Form data class
    public class CategoryFormData
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Slug { get; set; } = "";
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public SvgType IconSvgType { get; set; } = SvgType.None;
        public string? ParentId { get; set; }
        public int ProductCount { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}