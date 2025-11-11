using Microsoft.AspNetCore.Components;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Models.Firebase;
using SubashaVentures.Components.Shared.Modals;
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
    private CategoryModel editingCategory = new();
    private CategoryModel? categoryToDelete = null;
    private Dictionary<string, string> validationErrors = new();

    private DynamicModal? categoryModal;
    private ConfirmationPopup? confirmationPopup;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await LoadCategoriesAsync();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "CategoryManagement initialization");
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
        editingCategory = new CategoryModel
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
        editingCategory = new CategoryModel
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            IconEmoji = category.IconEmoji,
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

            // Auto-generate slug if empty
            if (string.IsNullOrEmpty(editingCategory.Slug))
            {
                editingCategory = editingCategory with
                {
                    Slug = GenerateSlug(editingCategory.Name)
                };
            }

            bool success;
            if (isEditMode)
            {
                editingCategory = editingCategory with { UpdatedAt = DateTime.UtcNow };
                success = await FirestoreService.UpdateDocumentAsync("categories", editingCategory.Id, editingCategory);
            }
            else
            {
                var id = await FirestoreService.AddDocumentAsync("categories", editingCategory, editingCategory.Id);
                success = !string.IsNullOrEmpty(id);
            }

            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ Category {(isEditMode ? "updated" : "created")}: {editingCategory.Name}",
                    LogLevel.Info
                );

                await LoadCategoriesAsync();
                CloseCategoryModal();
            }
            else
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Failed to save category",
                    LogLevel.Error
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Saving category");
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

                await LoadCategoriesAsync();
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Deleting category");
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
}
