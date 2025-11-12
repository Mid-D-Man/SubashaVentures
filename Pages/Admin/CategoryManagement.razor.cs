// Pages/Admin/CategoryManagement.razor.cs - FIXED
using Microsoft.AspNetCore.Components;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Models.Firebase;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Components.Shared.Notifications;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Admin;

public partial class CategoryManagement : ComponentBase
{
    [Inject] private IFirestoreService FirestoreService { get; set; } = default!;
    [Inject] private ILogger<CategoryManagement> Logger { get; set; } = default!;

    private bool isLoading = true;
    private bool isCategoryModalOpen = false;
    private bool isEditMode = false;
    private bool isSaving = false;
    private bool isConfirmationOpen = false;
    private bool isDeleting = false;

    private List<CategoryModel> categories = new();
    private CategoryFormData editingCategory = new();
    private CategoryModel? categoryToDelete = null;
    private Dictionary<string, string> validationErrors = new();

    private DynamicModal? categoryModal;
    private ConfirmationPopup? confirmationPopup;
    private NotificationComponent? notificationComponent;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await LoadCategoriesAsync();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "CategoryManagement initialization");
            ShowError("Failed to initialize category management");
        }
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            var loadedCategories = await FirestoreService.GetCollectionAsync<CategoryModel>("categories");
            
            if (loadedCategories != null)
            {
                categories = loadedCategories;
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Loaded {categories.Count} categories",
                    LogLevel.Info
                );
            }
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

    private void OpenAddCategoryModal()
    {
        isEditMode = false;
        editingCategory = new CategoryFormData
        {
            Id = Guid.NewGuid().ToString(),
            IsActive = true,
            DisplayOrder = categories.Count,
            CreatedAt = DateTime.UtcNow
        };
        validationErrors.Clear();
        isCategoryModalOpen = true;
        StateHasChanged();
    }

    private void OpenEditCategoryModal(CategoryModel category)
    {
        isEditMode = true;
        editingCategory = new CategoryFormData
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            IconEmoji = category.IconEmoji,
            ImageUrl = category.ImageUrl,
            ParentId = category.ParentId,
            DisplayOrder = category.DisplayOrder,
            IsActive = category.IsActive,
            ProductCount = category.ProductCount,
            CreatedAt = category.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };
        validationErrors.Clear();
        isCategoryModalOpen = true;
        StateHasChanged();
    }

    private void CloseCategoryModal()
    {
        isCategoryModalOpen = false;
        editingCategory = new();
        validationErrors.Clear();
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

            if (string.IsNullOrEmpty(editingCategory.Slug))
            {
                editingCategory.Slug = GenerateSlug(editingCategory.Name);
            }

            var categoryModel = new CategoryModel
            {
                Id = editingCategory.Id,
                Name = editingCategory.Name,
                Slug = editingCategory.Slug,
                Description = editingCategory.Description,
                ImageUrl = editingCategory.ImageUrl,
                IconEmoji = editingCategory.IconEmoji,
                ParentId = editingCategory.ParentId,
                ProductCount = editingCategory.ProductCount,
                DisplayOrder = editingCategory.DisplayOrder,
                IsActive = editingCategory.IsActive,
                CreatedAt = editingCategory.CreatedAt,
                UpdatedAt = editingCategory.UpdatedAt
            };

            bool success;
            if (isEditMode)
            {
                success = await FirestoreService.UpdateDocumentAsync("categories", categoryModel.Id, categoryModel);
            }
            else
            {
                var id = await FirestoreService.AddDocumentAsync("categories", categoryModel, categoryModel.Id);
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

    private string GenerateSlug(string name)
    {
        return name
            .ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("'", "")
            .Replace("&", "and");
    }

    private void HandleDeleteCategory(CategoryModel category)
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

        categoryToDelete = category;
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

            var success = await FirestoreService.DeleteDocumentAsync("categories", categoryToDelete.Id);

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

    // Form data class (not a record)
    public class CategoryFormData
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Slug { get; set; } = "";
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public string? IconEmoji { get; set; }
        public string? ParentId { get; set; }
        public int ProductCount { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
