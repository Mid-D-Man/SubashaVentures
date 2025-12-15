// Pages/Admin/MiscManagement.razor.cs
using Microsoft.AspNetCore.Components;
using SubashaVentures.Services.Brands;
using SubashaVentures.Models.Firebase;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Components.Shared.Notifications;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Admin;

public partial class MiscManagement : ComponentBase
{
    [Inject] private IBrandService BrandService { get; set; } = default!;
    [Inject] private ILogger<MiscManagement> Logger { get; set; } = default!;

    private string activeTab = "brands";
    private bool isLoading = true;
    private bool isBrandModalOpen = false;
    private bool isEditMode = false;
    private bool isSaving = false;
    private bool isConfirmationOpen = false;
    private bool isDeleting = false;

    private List<BrandModel> brands = new();
    private BrandFormData editingBrand = new();
    private BrandModel? brandToDelete = null;
    private Dictionary<string, string> validationErrors = new();

    private DynamicModal? brandModal;
    private ConfirmationPopup? confirmationPopup;
    private NotificationComponent? notificationComponent;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await LoadBrandsAsync();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "MiscManagement initialization");
            ShowError("Failed to initialize");
        }
    }

    private void SetActiveTab(string tab)
    {
        activeTab = tab;
        StateHasChanged();
    }

    private async Task LoadBrandsAsync()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            brands = await BrandService.GetAllBrandsAsync();
            
            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded {brands.Count} brands",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading brands");
            ShowError("Failed to load brands");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private void OpenAddBrandModal()
    {
        isEditMode = false;
        editingBrand = new BrandFormData
        {
            IsActive = true,
            DisplayOrder = brands.Count
        };
        validationErrors.Clear();
        isBrandModalOpen = true;
        StateHasChanged();
    }

    private void OpenEditBrandModal(BrandModel brand)
    {
        isEditMode = true;
        editingBrand = new BrandFormData
        {
            Id = brand.Id,
            Name = brand.Name,
            Description = brand.Description,
            LogoUrl = brand.LogoUrl,
            WebsiteUrl = brand.WebsiteUrl,
            DisplayOrder = brand.DisplayOrder,
            IsFeatured = brand.IsFeatured,
            IsActive = brand.IsActive
        };
        validationErrors.Clear();
        isBrandModalOpen = true;
        StateHasChanged();
    }

    private void CloseBrandModal()
    {
        isBrandModalOpen = false;
        editingBrand = new();
        validationErrors.Clear();
        StateHasChanged();
    }

    private async Task SaveBrand()
    {
        try
        {
            if (!ValidateBrand())
                return;

            isSaving = true;
            StateHasChanged();

            bool success;
            if (isEditMode)
            {
                var updateRequest = new UpdateBrandRequest
                {
                    Name = editingBrand.Name,
                    Description = editingBrand.Description,
                    LogoUrl = editingBrand.LogoUrl,
                    WebsiteUrl = editingBrand.WebsiteUrl,
                    DisplayOrder = editingBrand.DisplayOrder,
                    IsFeatured = editingBrand.IsFeatured,
                    IsActive = editingBrand.IsActive
                };
                
                success = await BrandService.UpdateBrandAsync(editingBrand.Id, updateRequest);
            }
            else
            {
                var createRequest = new CreateBrandRequest
                {
                    Name = editingBrand.Name,
                    Description = editingBrand.Description,
                    LogoUrl = editingBrand.LogoUrl,
                    WebsiteUrl = editingBrand.WebsiteUrl,
                    DisplayOrder = editingBrand.DisplayOrder,
                    IsFeatured = editingBrand.IsFeatured
                };
                
                var id = await BrandService.CreateBrandAsync(createRequest);
                success = !string.IsNullOrEmpty(id);
            }

            if (success)
            {
                ShowSuccess($"Brand '{editingBrand.Name}' {(isEditMode ? "updated" : "created")} successfully");
                await LoadBrandsAsync();
                CloseBrandModal();
            }
            else
            {
                ShowError("Failed to save brand");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Saving brand");
            ShowError("An error occurred while saving");
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    private bool ValidateBrand()
    {
        validationErrors.Clear();

        if (string.IsNullOrWhiteSpace(editingBrand.Name))
        {
            validationErrors["Name"] = "Brand name is required";
        }

        if (validationErrors.Any())
        {
            StateHasChanged();
            return false;
        }

        return true;
    }

    private void HandleDeleteBrand(BrandModel brand)
    {
        if (brand.ProductCount > 0)
        {
            ShowWarning("Cannot delete brand with products");
            return;
        }

        brandToDelete = brand;
        isConfirmationOpen = true;
        StateHasChanged();
    }

    private async Task ConfirmDeleteBrand()
    {
        if (brandToDelete == null)
            return;

        try
        {
            isDeleting = true;
            StateHasChanged();

            var success = await BrandService.DeleteBrandAsync(brandToDelete.Id);

            if (success)
            {
                ShowSuccess($"Brand '{brandToDelete.Name}' deleted successfully");
                await LoadBrandsAsync();
            }
            else
            {
                ShowError("Failed to delete brand");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Deleting brand");
            ShowError("An error occurred while deleting");
        }
        finally
        {
            isDeleting = false;
            isConfirmationOpen = false;
            brandToDelete = null;
            StateHasChanged();
        }
    }

    private void CancelDeleteBrand()
    {
        isConfirmationOpen = false;
        brandToDelete = null;
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

    public class BrandFormData
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public string? WebsiteUrl { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
