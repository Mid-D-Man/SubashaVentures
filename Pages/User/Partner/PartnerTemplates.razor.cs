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
using SubashaVentures.Services.Users;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Components.Shared.Notifications;

namespace SubashaVentures.Pages.User.Partner;

public partial class PartnerTemplates : ComponentBase
{
    [Inject] private IPartnerTemplateService     PartnerTemplateService { get; set; } = default!;
    [Inject] private ICloudflareR2Service        R2Service              { get; set; } = default!;
    [Inject] private ICategoryService            CategoryService        { get; set; } = default!;
    [Inject] private IUserService                UserService            { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider      { get; set; } = default!;
    [Inject] private NavigationManager           Navigation             { get; set; } = default!;

    // ── State ─────────────────────────────────────────────────────────────────

    private bool   isLoading         = true;
    private bool   isModalOpen       = false;
    private bool   isEditMode        = false;
    private bool   isSaving          = false;
    private bool   isUploading       = false;
    private bool   showDeleteConfirm = false;
    private int    modalStep         = 1;
    private int    uploadingCount    = 0;
    private string userId            = string.Empty;
    private string partnerId         = string.Empty;
    private string activeTab         = "all";
    private string tagsInput         = string.Empty;

    // ── Variant inline form state ──────────────────────────────────────────────
    private bool            isAddingVariant    = false;
    private int             editingVariantIndex = -1;
    private VariantEditData activeVariantForm  = new();
    private Dictionary<string, string> variantErrors = new();

    // ── Data ──────────────────────────────────────────────────────────────────
    private List<PartnerTemplateViewModel> allTemplates        = new();
    private List<PartnerTemplateViewModel> filteredTemplates   = new();
    private List<CategoryViewModel>        availableCategories = new();

    private PartnerTemplateViewModel?  templateToDelete = null;
    private TemplateFormData           templateForm     = new();
    private Dictionary<string, string> formErrors       = new();

    private DynamicModal?          templateModal;
    private ConfirmationPopup?     deleteConfirmation;
    private NotificationComponent? notificationComponent;

    private record TabInfo(string Key, string Label, int Count);
    private List<TabInfo> tabs = new();

    // ── Computed ───────────────────────────────────────────────────────────────

    private IEnumerable<string> ParsedTags =>
        string.IsNullOrWhiteSpace(tagsInput)
            ? Enumerable.Empty<string>()
            : tagsInput.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(t => t.Trim())
                       .Where(t => !string.IsNullOrEmpty(t))
                       .Distinct();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var user      = authState.User;

            if (user.Identity?.IsAuthenticated != true)
            {
                Navigation.NavigateTo("signin");
                return;
            }

            userId    = user.FindFirst("sub")?.Value ?? user.FindFirst("id")?.Value ?? string.Empty;
            partnerId = user.FindFirst("partner_id")?.Value ?? string.Empty;

            if (string.IsNullOrEmpty(partnerId))
            {
                try
                {
                    var dbProfile = await UserService.GetUserByIdAsync(userId);
                    if (dbProfile?.IsPartner == true)
                        partnerId = dbProfile.PartnerId ?? string.Empty;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"PartnerTemplates DB partnerId fallback: {ex.Message}");
                }
            }

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

    // ── Data ──────────────────────────────────────────────────────────────────

    private async Task LoadTemplatesAsync()
    {
        allTemplates = await PartnerTemplateService.GetPartnerTemplatesAsync(partnerId);
        BuildTabs();
        ApplyTabFilter();
    }

    private async Task LoadCategoriesAsync()
    {
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

    // ── Modal open/close ──────────────────────────────────────────────────────

    private void OpenCreateModal()
    {
        isEditMode    = false;
        modalStep     = 1;
        templateForm  = new TemplateFormData();
        tagsInput     = string.Empty;
        formErrors.Clear();
        ResetVariantForm();
        isModalOpen = true;
    }

    private void OpenEditModal(PartnerTemplateViewModel template)
    {
        isEditMode = true;
        modalStep  = 1;
        formErrors.Clear();
        ResetVariantForm();

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
        isModalOpen = false;
        ResetVariantForm();
    }

    // ── Step navigation ───────────────────────────────────────────────────────

    private void TryNavigateToStep(int step)
    {
        // Only allow going back, or to already-validated steps
        if (step < modalStep) modalStep = step;
    }

    private void GoToStep2()
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
            formErrors["ProposedPrice"] = "Enter a valid price greater than zero";

        if (formErrors.Any()) return;

        var cat = availableCategories.FirstOrDefault(c => c.Id == templateForm.CategoryId);
        if (cat != null) templateForm.CategoryName = cat.Name;

        templateForm.Tags = ParsedTags.ToList();
        ResetVariantForm();
        modalStep = 2;
    }

    private void GoToStep3()
    {
        formErrors.Clear();

        // Warn about unsaved variant form
        if (isAddingVariant || editingVariantIndex >= 0)
        {
            formErrors["Variants"] = "Please save or cancel the open variant form before continuing.";
            StateHasChanged();
            return;
        }

        if (!templateForm.Variants.Any())
        {
            formErrors["Variants"] = "Add at least one variant before proceeding.";
            StateHasChanged();
            return;
        }

        var skus = templateForm.Variants.Select(v => v.Sku?.Trim()).ToList();
        if (skus.Any(string.IsNullOrEmpty))
        {
            formErrors["Variants"] = "All variants must have a SKU.";
            return;
        }
        if (skus.Distinct(StringComparer.OrdinalIgnoreCase).Count() != skus.Count)
        {
            formErrors["Variants"] = "Variant SKUs must be unique.";
            return;
        }
        if (templateForm.Variants.Any(v => v.Stock < 0))
        {
            formErrors["Variants"] = "Variant stock cannot be negative.";
            return;
        }

        modalStep = 3;
    }

    // ── Variant management ────────────────────────────────────────────────────

    private void StartAddVariant()
    {
        editingVariantIndex = -1;
        activeVariantForm   = new VariantEditData();
        variantErrors.Clear();
        isAddingVariant     = true;
        StateHasChanged();
    }

    private void StartEditVariant(int index)
    {
        if (index < 0 || index >= templateForm.Variants.Count) return;
        isAddingVariant     = false;
        editingVariantIndex = index;
        variantErrors.Clear();

        var v = templateForm.Variants[index];
        activeVariantForm = new VariantEditData
        {
            Sku             = v.Sku ?? string.Empty,
            Size            = v.Size,
            Color           = v.Color,
            Stock           = v.Stock,
            PriceAdjustment = v.PriceAdjustment
        };
        StateHasChanged();
    }

    private void SaveVariant()
    {
        variantErrors.Clear();

        if (string.IsNullOrWhiteSpace(activeVariantForm.Sku))
            variantErrors["Sku"] = "SKU is required.";

        if (activeVariantForm.Stock < 0)
            variantErrors["Stock"] = "Stock cannot be negative.";

        // Check SKU uniqueness (excluding current editing index)
        if (!variantErrors.ContainsKey("Sku") && !string.IsNullOrWhiteSpace(activeVariantForm.Sku))
        {
            var normalized = activeVariantForm.Sku.Trim();
            for (int i = 0; i < templateForm.Variants.Count; i++)
            {
                if (i == editingVariantIndex) continue;
                if (string.Equals(templateForm.Variants[i].Sku, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    variantErrors["Sku"] = "This SKU is already used by another variant.";
                    break;
                }
            }
        }

        if (variantErrors.Any()) { StateHasChanged(); return; }

        var variant = new PartnerTemplateVariantViewModel
        {
            Sku             = activeVariantForm.Sku.Trim(),
            Size            = string.IsNullOrWhiteSpace(activeVariantForm.Size) ? null : activeVariantForm.Size.Trim(),
            Color           = string.IsNullOrWhiteSpace(activeVariantForm.Color) ? null : activeVariantForm.Color.Trim(),
            Stock           = Math.Max(0, activeVariantForm.Stock),
            PriceAdjustment = activeVariantForm.PriceAdjustment
        };

        if (isAddingVariant)
        {
            templateForm.Variants.Add(variant);
            isAddingVariant = false;
        }
        else if (editingVariantIndex >= 0 && editingVariantIndex < templateForm.Variants.Count)
        {
            templateForm.Variants[editingVariantIndex] = variant;
            editingVariantIndex = -1;
        }

        activeVariantForm = new VariantEditData();
        variantErrors.Clear();
        StateHasChanged();
    }

    private void CancelVariant()
    {
        ResetVariantForm();
        StateHasChanged();
    }

    private void RemoveVariant(int index)
    {
        if (index < 0 || index >= templateForm.Variants.Count) return;
        templateForm.Variants.RemoveAt(index);
        // Reset editing state if we removed the item being edited
        if (editingVariantIndex == index) ResetVariantForm();
        StateHasChanged();
    }

    private void GenerateVariantSkuForActive()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(activeVariantForm.Size))
            parts.Add(activeVariantForm.Size.Trim().ToUpperInvariant().Replace(" ", ""));
        if (!string.IsNullOrWhiteSpace(activeVariantForm.Color))
            parts.Add(activeVariantForm.Color.Trim().ToUpperInvariant().Replace(" ", "")[..Math.Min(3, activeVariantForm.Color.Trim().Length)]);

        var suffix  = parts.Any() ? string.Join("-", parts) : "VAR";
        var date    = DateTime.UtcNow.ToString("MMdd");
        var random  = Guid.NewGuid().ToString("N")[..4].ToUpper();
        activeVariantForm.Sku = $"{suffix}-{date}-{random}";
        StateHasChanged();
    }

    private void ResetVariantForm()
    {
        isAddingVariant     = false;
        editingVariantIndex = -1;
        activeVariantForm   = new VariantEditData();
        variantErrors.Clear();
    }

    // ── Image handling ────────────────────────────────────────────────────────

    private async Task HandleImageUpload(InputFileChangeEventArgs e)
    {
        var remaining = 10 - templateForm.ImageUrls.Count;
        if (remaining <= 0) return;

        var files = e.GetMultipleFiles(remaining);
        if (!files.Any()) return;

        isUploading    = true;
        uploadingCount = files.Count;
        StateHasChanged();

        // Ensure we have a template ID to attach images to
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

                var objectKey = R2Service.BuildTemplateImageKey(partnerId, templateForm.TemplateId!, file.Name);
                var result    = await R2Service.UploadFileAsync(file, objectKey, file.ContentType);

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
        if (index < 0 || index >= templateForm.ImageUrls.Count) return;
        templateForm.ImageUrls.RemoveAt(index);
        StateHasChanged();
    }

    private async Task SaveDraftSilentlyAsync()
    {
        var request = BuildSaveRequest();
        var result  = await PartnerTemplateService.SaveDraftAsync(partnerId, userId, request);
        if (result != null && string.IsNullOrEmpty(templateForm.TemplateId))
            templateForm.TemplateId = result.Id;
    }

    // ── Save / submit ─────────────────────────────────────────────────────────

    private async Task HandleSaveDraft()
    {
        formErrors.Clear();

        if (!templateForm.ImageUrls.Any())
        {
            formErrors["ImageUrls"] = "Upload at least one product image.";
            StateHasChanged();
            return;
        }

        isSaving = true;
        StateHasChanged();

        try
        {
            var result = await PartnerTemplateService.SaveDraftAsync(partnerId, userId, BuildSaveRequest());
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

    private SaveTemplateRequest BuildSaveRequest() => new SaveTemplateRequest
    {
        TemplateId            = string.IsNullOrEmpty(templateForm.TemplateId) ? null : templateForm.TemplateId,
        TemplateName          = templateForm.TemplateName.Trim(),
        ProductName           = templateForm.ProductName.Trim(),
        Description           = templateForm.Description.Trim(),
        CategoryId            = templateForm.CategoryId,
        CategoryName          = templateForm.CategoryName,
        ProposedPrice         = templateForm.ProposedPrice,
        ProposedOriginalPrice = templateForm.ProposedOriginalPrice > 0 ? templateForm.ProposedOriginalPrice : null,
        WeightKg              = templateForm.WeightKg,
        HasFreeShipping       = templateForm.HasFreeShipping,
        ImageUrls             = templateForm.ImageUrls,
        Tags                  = templateForm.Tags.Any() ? templateForm.Tags : ParsedTags.ToList(),
        Variants              = templateForm.Variants,
    };

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
        catch (Exception ex) { notificationComponent?.ShowError($"Error: {ex.Message}"); }
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
        catch (Exception ex) { notificationComponent?.ShowError($"Error: {ex.Message}"); }
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
        catch (Exception ex) { notificationComponent?.ShowError($"Error: {ex.Message}"); }
        finally
        {
            isSaving          = false;
            showDeleteConfirm = false;
            templateToDelete  = null;
            StateHasChanged();
        }
    }

    // ── Validation helpers ─────────────────────────────────────────────────────

    private bool   HasFormError(string field)    => formErrors.ContainsKey(field);
    private string GetFormError(string field)    => formErrors.GetValueOrDefault(field, string.Empty);
    private bool   HasVariantError(string field) => variantErrors.ContainsKey(field);
    private string GetVariantError(string field) => variantErrors.GetValueOrDefault(field, string.Empty);

    // ── Inner types ────────────────────────────────────────────────────────────

    public class TemplateFormData
    {
        public string?  TemplateId            { get; set; }
        public string   TemplateName          { get; set; } = string.Empty;
        public string   ProductName           { get; set; } = string.Empty;
        public string   Description           { get; set; } = string.Empty;
        public string   CategoryId            { get; set; } = string.Empty;
        public string   CategoryName          { get; set; } = string.Empty;
        public decimal  ProposedPrice         { get; set; }
        public decimal  ProposedOriginalPrice { get; set; }
        public decimal  WeightKg              { get; set; } = 1.0m;
        public bool     HasFreeShipping       { get; set; }
        public List<string> ImageUrls         { get; set; } = new();
        public List<string> Tags              { get; set; } = new();
        public List<PartnerTemplateVariantViewModel> Variants { get; set; } = new();
    }

    public class VariantEditData
    {
        public string   Sku             { get; set; } = string.Empty;
        public string?  Size            { get; set; }
        public string?  Color           { get; set; }
        public int      Stock           { get; set; }
        public decimal  PriceAdjustment { get; set; }
    }
}
