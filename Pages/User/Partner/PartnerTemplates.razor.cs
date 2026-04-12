// Pages/User/Partner/PartnerTemplates.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using SubashaVentures.Domain.Partner;
using SubashaVentures.Domain.Product;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Services.Categories;
using SubashaVentures.Services.Partners;
using SubashaVentures.Services.Storage;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Components.Shared.Notifications;

namespace SubashaVentures.Pages.User.Partner;

public partial class PartnerTemplates : ComponentBase
{
    [Inject] private IPartnerTemplateService PartnerTemplateService { get; set; } = default!;
    [Inject] private ICloudflareR2Service R2Service { get; set; } = default!;
    [Inject] private ICategoryService CategoryService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private bool isLoading = true;
    private bool isModalOpen = false;
    private bool isEditMode = false;
    private bool isSaving = false;
    private bool isUploading = false;
    private bool showDeleteConfirm = false;
    private int modalStep = 1;
    private int uploadingCount = 0;

    private string userId = string.Empty;
    private string partnerId = string.Empty;
    private string activeTab = "all";
    private string tagsInput = string.Empty;

    private List<PartnerTemplateViewModel> allTemplates = new();
    private List<PartnerTemplateViewModel> filteredTemplates = new();
    private List<CategoryViewModel> availableCategories = new();

    private PartnerTemplateViewModel? templateToDelete = null;
    private TemplateFormData templateForm = new();
    private Dictionary<string, string> formErrors = new();

    private DynamicModal? templateModal;
    private ConfirmationPopup? deleteConfirmation;
    private NotificationComponent? notificationComponent;

    private record TabInfo(string Key, string Label, int Count);
    private List<TabInfo> tabs = new();

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (user.Identity?.IsAuthenticated != true)
            {
                Navigation.NavigateTo("signin");
                return;
            }

            userId    = user.FindFirst("sub")?.Value ?? user.FindFirst("id")?.Value ?? string.Empty;
            partnerId = user.FindFirst("partner_id")?.Value ?? string.Empty;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(partnerId))
            {
                Navigation.NavigateTo("user/partner/dashboard");
                return;
            }

            await Task.WhenAll(LoadTemplatesAsync(), LoadCategoriesAsync());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PartnerTemplates init error: {ex.Message}");
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task LoadTemplatesAsync()
    {
        allTemplates = await PartnerTemplateService.GetPartnerTemplatesAsync(partnerId);
        BuildTabs();
        ApplyTabFilter();
    }

    private async Task LoadCategoriesAsync()
    {
        // FIX: was GetActiveCategoriesAsync — now correctly calls the interface method
        availableCategories = await CategoryService.GetActiveCategoriesAsync();
    }

    private void BuildTabs()
    {
        tabs = new List<TabInfo>
        {
            new("all",            "All",            allTemplates.Count),
            new("draft",          "Drafts",         allTemplates.Count(t => t.IsDraft)),
            new("pending_review", "Pending Review", allTemplates.Count(t => t.IsPendingReview)),
            new("approved",       "Approved",       allTemplates.Count(t => t.IsApproved)),
            new("rejected",       "Rejected",       allTemplates.Count(t => t.IsRejected)),
        };
    }

    private void SetTab(string tab)
    {
        activeTab = tab;
        ApplyTabFilter();
    }

    private void ApplyTabFilter()
    {
        filteredTemplates = activeTab == "all"
            ? allTemplates.ToList()
            : allTemplates.Where(t => t.Status == activeTab).ToList();

        StateHasChanged();
    }

    // ── Modal ──────────────────────────────────────────────────

    private void OpenCreateModal()
    {
        isEditMode   = false;
        modalStep    = 1;
        templateForm = new TemplateFormData();
        tagsInput    = string.Empty;
        formErrors.Clear();
        isModalOpen  = true;
    }

    private void OpenEditModal(PartnerTemplateViewModel template)
    {
        isEditMode = true;
        modalStep  = 1;
        formErrors.Clear();

        // FIX: ProposedOriginalPrice is decimal? in VM but decimal in form — use ?? 0m
        templateForm = new TemplateFormData
        {
            TemplateId            = template.Id,
            TemplateName          = template.TemplateName,
            ProductName           = template.ProductName,
            Description           = template.Description,
            CategoryId            = template.CategoryId,
            CategoryName          = template.CategoryName,
            ProposedPrice         = template.ProposedPrice,
            ProposedOriginalPrice = template.ProposedOriginalPrice ?? 0m,
            WeightKg              = template.WeightKg,
            HasFreeShipping       = template.HasFreeShipping,
            ImageUrls             = template.ImageUrls.ToList(),
            Variants              = template.Variants.Select(v => new PartnerTemplateVariantViewModel
            {
                Sku             = v.Sku,
                Size            = v.Size,
                Color           = v.Color,
                Stock           = v.Stock,
                PriceAdjustment = v.PriceAdjustment,
            }).ToList(),
        };

        tagsInput   = string.Join(", ", template.Tags);
        isModalOpen = true;
    }

    private void CloseModal()
    {
        isModalOpen  = false;
        templateForm = new TemplateFormData();
        formErrors.Clear();
    }

    // ── Step navigation ────────────────────────────────────────

    private void GoToModalStep2()
    {
        formErrors.Clear();

        if (string.IsNullOrWhiteSpace(templateForm.TemplateName))
            formErrors["TemplateName"] = "Template name is required";

        if (string.IsNullOrWhiteSpace(templateForm.ProductName))
            formErrors["ProductName"] = "Product name is required";

        if (string.IsNullOrWhiteSpace(templateForm.Description) || templateForm.Description.Trim().Length < 20)
            formErrors["Description"] = "Description must be at least 20 characters";

        if (string.IsNullOrWhiteSpace(templateForm.CategoryId))
            formErrors["CategoryId"] = "Select a category";

        if (templateForm.ProposedPrice <= 0)
            formErrors["ProposedPrice"] = "Enter a valid price";

        if (formErrors.Any()) return;

        var cat = availableCategories.FirstOrDefault(c => c.Id == templateForm.CategoryId);
        if (cat != null) templateForm.CategoryName = cat.Name;

        templateForm.Tags = tagsInput
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();

        modalStep = 2;
    }

    private void GoToModalStep3()
    {
        formErrors.Clear();

        if (!templateForm.Variants.Any())
        {
            formErrors["Variants"] = "Add at least one variant";
            return;
        }

        var skus = templateForm.Variants.Select(v => v.Sku?.Trim()).ToList();
        if (skus.Any(string.IsNullOrEmpty))
        {
            formErrors["Variants"] = "All variants must have a SKU";
            return;
        }

        if (skus.Distinct().Count() != skus.Count)
        {
            formErrors["Variants"] = "Variant SKUs must be unique";
            return;
        }

        if (templateForm.Variants.Any(v => v.Stock < 0))
        {
            formErrors["Variants"] = "Stock cannot be negative";
            return;
        }

        modalStep = 3;
    }

    // ── Variants ───────────────────────────────────────────────

    private void AddVariant()
    {
        templateForm.Variants.Add(new PartnerTemplateVariantViewModel());
        StateHasChanged();
    }

    private void RemoveVariant(int index)
    {
        if (templateForm.Variants.Count > 1)
            templateForm.Variants.RemoveAt(index);
        StateHasChanged();
    }

    // ── Images ─────────────────────────────────────────────────

    private async Task HandleImageUpload(InputFileChangeEventArgs e)
    {
        var files = e.GetMultipleFiles(10 - templateForm.ImageUrls.Count);
        if (!files.Any()) return;

        isUploading    = true;
        uploadingCount = files.Count;
        StateHasChanged();

        if (string.IsNullOrEmpty(templateForm.TemplateId))
            await SaveDraftSilentlyAsync();

        foreach (var file in files)
        {
            try
            {
                var validation = R2Service.ValidateImageFile(file);
                if (!validation.IsValid)
                {
                    notificationComponent?.ShowWarning($"{file.Name}: {validation.Errors.First()}");
                    continue;
                }

                var objectKey = R2Service.BuildTemplateImageKey(
                    partnerId,
                    templateForm.TemplateId!,
                    file.Name);

                var result = await R2Service.UploadFileAsync(file, objectKey, file.ContentType);

                if (result.Success && !string.IsNullOrEmpty(result.PublicUrl))
                    templateForm.ImageUrls.Add(result.PublicUrl);
                else
                    notificationComponent?.ShowError($"Failed to upload {file.Name}");
            }
            catch (Exception ex)
            {
                notificationComponent?.ShowError($"Upload error: {ex.Message}");
            }
        }

        isUploading    = false;
        uploadingCount = 0;
        StateHasChanged();
    }

    private void RemoveImage(int index)
    {
        templateForm.ImageUrls.RemoveAt(index);
        StateHasChanged();
    }

    // ── Save draft ─────────────────────────────────────────────

    private async Task SaveDraftSilentlyAsync()
    {
        var request = BuildSaveRequest();
        var result  = await PartnerTemplateService.SaveDraftAsync(partnerId, userId, request);
        if (result != null && string.IsNullOrEmpty(templateForm.TemplateId))
            templateForm.TemplateId = result.Id;
    }

    private async Task HandleSaveDraft()
    {
        formErrors.Clear();

        if (!templateForm.ImageUrls.Any())
        {
            formErrors["ImageUrls"] = "Upload at least one image";
            StateHasChanged();
            return;
        }

        isSaving = true;
        StateHasChanged();

        try
        {
            var request = BuildSaveRequest();
            var result  = await PartnerTemplateService.SaveDraftAsync(partnerId, userId, request);

            if (result != null)
            {
                notificationComponent?.ShowSuccess("Draft saved successfully!");
                CloseModal();
                await LoadTemplatesAsync();
            }
            else
            {
                notificationComponent?.ShowError("Failed to save draft. Please try again.");
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error: {ex.Message}");
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    private SaveTemplateRequest BuildSaveRequest()
    {
        return new SaveTemplateRequest
        {
            TemplateId            = string.IsNullOrEmpty(templateForm.TemplateId) ? null : templateForm.TemplateId,
            TemplateName          = templateForm.TemplateName.Trim(),
            ProductName           = templateForm.ProductName.Trim(),
            Description           = templateForm.Description.Trim(),
            CategoryId            = templateForm.CategoryId,
            CategoryName          = templateForm.CategoryName,
            ProposedPrice         = templateForm.ProposedPrice,
            // FIX: only pass optional original price when it has a meaningful value
            ProposedOriginalPrice = templateForm.ProposedOriginalPrice > 0
                ? templateForm.ProposedOriginalPrice
                : null,
            WeightKg              = templateForm.WeightKg,
            HasFreeShipping       = templateForm.HasFreeShipping,
            ImageUrls             = templateForm.ImageUrls,
            Tags                  = templateForm.Tags,
            Variants              = templateForm.Variants,
        };
    }

    // ── Submit / Resubmit / Delete ─────────────────────────────

    private async Task HandleSubmitForReview(PartnerTemplateViewModel template)
    {
        try
        {
            var result = await PartnerTemplateService.SubmitForReviewAsync(template.Id, partnerId);
            if (result.Success)
            {
                notificationComponent?.ShowSuccess("Template submitted for review!");
                await LoadTemplatesAsync();
            }
            else
            {
                var msg = result.ValidationErrors.Any()
                    ? string.Join(", ", result.ValidationErrors)
                    : result.ErrorMessage ?? "Submission failed";
                notificationComponent?.ShowError(msg);
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error: {ex.Message}");
        }
    }

    private async Task HandleResubmit(PartnerTemplateViewModel template)
    {
        try
        {
            var result = await PartnerTemplateService.ResubmitAsync(template.Id, partnerId);
            if (result.Success)
            {
                notificationComponent?.ShowSuccess("Template resubmitted for review!");
                await LoadTemplatesAsync();
            }
            else
            {
                notificationComponent?.ShowError(result.ErrorMessage ?? "Resubmission failed");
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error: {ex.Message}");
        }
    }

    private void HandleDeleteDraft(PartnerTemplateViewModel template)
    {
        templateToDelete  = template;
        showDeleteConfirm = true;
        StateHasChanged();
    }

    private async Task ConfirmDeleteDraft()
    {
        if (templateToDelete == null) return;

        isSaving = true;
        StateHasChanged();

        try
        {
            var success = await PartnerTemplateService.DeleteDraftAsync(templateToDelete.Id, partnerId);
            if (success)
            {
                notificationComponent?.ShowSuccess("Draft deleted.");
                await LoadTemplatesAsync();
            }
            else
            {
                notificationComponent?.ShowError("Failed to delete draft.");
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error: {ex.Message}");
        }
        finally
        {
            isSaving          = false;
            showDeleteConfirm = false;
            templateToDelete  = null;
            StateHasChanged();
        }
    }

    // ── Helpers ────────────────────────────────────────────────

    private bool HasFormError(string field)   => formErrors.ContainsKey(field);
    private string GetFormError(string field) => formErrors.GetValueOrDefault(field, string.Empty);

    // ── Form model ─────────────────────────────────────────────

    public class TemplateFormData
    {
        public string?  TemplateId            { get; set; }
        public string   TemplateName          { get; set; } = string.Empty;
        public string   ProductName           { get; set; } = string.Empty;
        public string   Description           { get; set; } = string.Empty;
        public string   CategoryId            { get; set; } = string.Empty;
        public string   CategoryName          { get; set; } = string.Empty;
        public decimal  ProposedPrice         { get; set; }
        // FIX: decimal (not decimal?) — 0 means "no original price set"
        public decimal  ProposedOriginalPrice { get; set; }
        public decimal  WeightKg              { get; set; } = 1.0m;
        public bool     HasFreeShipping       { get; set; }
        public List<string> ImageUrls         { get; set; } = new();
        public List<string> Tags              { get; set; } = new();
        public List<PartnerTemplateVariantViewModel> Variants { get; set; } = new()
        {
            new PartnerTemplateVariantViewModel()
        };
    }
}
