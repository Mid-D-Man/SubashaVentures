using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Domain.Partner;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Services.Partners;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Components.Shared.Notifications;

namespace SubashaVentures.Pages.Admin;

public partial class PartnerTemplateReview : ComponentBase
{
    [Inject] private IPartnerTemplateService     TemplateService   { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private NavigationManager           Navigation        { get; set; } = default!;

    // ── State ──────────────────────────────────────────────────
    private bool isLoading    = true;
    private bool isDetailOpen = false;
    private bool isRejectOpen = false;
    private bool isActioning  = false;

    private string adminUserId   = string.Empty;
    private string searchQuery   = string.Empty;
    private string selectedStatus = string.Empty;
    private string sortBy         = "newest";
    private decimal? priceOverride = null;

    // ── Data ───────────────────────────────────────────────────
    private List<PartnerTemplateViewModel> all      = new();
    private List<PartnerTemplateViewModel> filtered = new();
    private TemplateStatistics             stats    = new();
    private PartnerTemplateViewModel?      selectedTemplate = null;

    // ── Forms ──────────────────────────────────────────────────
    private RejectTemplateFormData       rejectForm   = new();
    private Dictionary<string, string>   rejectErrors = new();

    // ── Refs ───────────────────────────────────────────────────
    private DynamicModal?          detailModal;
    private DynamicModal?          rejectModal;
    private NotificationComponent? notificationComponent;

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

            adminUserId = user.FindFirst("sub")?.Value
                       ?? user.FindFirst("id")?.Value
                       ?? string.Empty;

            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PartnerTemplateReview init: {ex.Message}");
            isLoading = false;
        }
    }

    private async Task LoadDataAsync()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            var allTask   = TemplateService.GetAllTemplatesAsync();
            var statsTask = TemplateService.GetTemplateStatisticsAsync();
            await Task.WhenAll(allTask, statsTask);

            all   = await allTask;
            stats = await statsTask;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PartnerTemplateReview load: {ex.Message}");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    // ── Filtering ──────────────────────────────────────────────

    private void ApplyFilter()
    {
        var q = all.Where(t =>
            (string.IsNullOrEmpty(searchQuery) ||
             t.ProductName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)  ||
             t.TemplateName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
             t.CategoryName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(selectedStatus) || t.Status == selectedStatus));

        filtered = sortBy switch
        {
            "oldest"    => q.OrderBy(t => t.CreatedAt).ToList(),
            "submitted" => q.OrderBy(t => t.SubmittedAt).ToList(),
            _           => q.OrderByDescending(t => t.CreatedAt).ToList()
        };

        StateHasChanged();
    }

    private void HandleStatusFilter(ChangeEventArgs e)
    {
        selectedStatus = e.Value?.ToString() ?? string.Empty;
        ApplyFilter();
    }

    private void HandleSortChange(ChangeEventArgs e)
    {
        sortBy = e.Value?.ToString() ?? "newest";
        ApplyFilter();
    }

    // ── Detail modal ───────────────────────────────────────────

    private void SelectTemplate(PartnerTemplateViewModel t)
    {
        selectedTemplate = t;
        priceOverride    = null;
        isDetailOpen     = true;
        StateHasChanged();
    }

    private void CloseDetail()
    {
        isDetailOpen     = false;
        selectedTemplate = null;
        priceOverride    = null;
        StateHasChanged();
    }

    // ── Approve ────────────────────────────────────────────────

    private async Task HandleApprove()
    {
        if (selectedTemplate == null) return;

        isActioning = true;
        StateHasChanged();

        try
        {
            var request = new ApproveTemplateRequest
            {
                TemplateId    = selectedTemplate.Id,
                AdminUserId   = adminUserId,
                PriceOverride = priceOverride > 0 ? priceOverride : null,
            };

            var result = await TemplateService.ApproveTemplateAsync(request);

            if (result.Success)
            {
                notificationComponent?.ShowSuccess(
                    $"Template approved! Product ID: {result.ApprovedProductId}");
                CloseDetail();
                await LoadDataAsync();
            }
            else
            {
                notificationComponent?.ShowError(
                    result.ErrorMessage ?? "Approval failed. Check edge function logs.");
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error: {ex.Message}");
        }
        finally
        {
            isActioning = false;
            StateHasChanged();
        }
    }

    // ── Reject ─────────────────────────────────────────────────

    private void OpenRejectModal()
    {
        rejectForm   = new RejectTemplateFormData();
        rejectErrors.Clear();
        isRejectOpen = true;
    }

    private void CloseRejectModal()
    {
        isRejectOpen = false;
        rejectForm   = new RejectTemplateFormData();
        rejectErrors.Clear();
    }

    private async Task HandleConfirmReject()
    {
        rejectErrors.Clear();

        if (string.IsNullOrWhiteSpace(rejectForm.Reason) ||
            rejectForm.Reason.Trim().Length < 10)
            rejectErrors["Reason"] =
                "Please provide a meaningful reason (minimum 10 characters)";

        if (rejectErrors.Any())
        {
            StateHasChanged();
            return;
        }

        if (selectedTemplate == null) return;

        isActioning = true;
        StateHasChanged();

        try
        {
            var fields = rejectForm.FieldsFlaggedInput
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .Where(f => !string.IsNullOrEmpty(f))
                .ToList();

            var request = new RejectTemplateRequest
            {
                TemplateId    = selectedTemplate.Id,
                AdminUserId   = adminUserId,
                Reason        = rejectForm.Reason.Trim(),
                FieldsFlagged = fields,
                AdminNotes    = string.IsNullOrWhiteSpace(rejectForm.AdminNotes)
                    ? null : rejectForm.AdminNotes.Trim()
            };

            var success = await TemplateService.RejectTemplateAsync(request);

            if (success)
            {
                notificationComponent?.ShowSuccess("Template rejected.");
                CloseRejectModal();
                CloseDetail();
                await LoadDataAsync();
            }
            else
            {
                notificationComponent?.ShowError("Failed to reject template.");
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error: {ex.Message}");
        }
        finally
        {
            isActioning = false;
            StateHasChanged();
        }
    }

    // ── Helpers ────────────────────────────────────────────────

    private bool   HasRejectError(string f) => rejectErrors.ContainsKey(f);
    private string GetRejectError(string f) => rejectErrors.GetValueOrDefault(f, string.Empty);

    // ── Form models ────────────────────────────────────────────

    public class RejectTemplateFormData
    {
        public string Reason             { get; set; } = string.Empty;
        public string FieldsFlaggedInput { get; set; } = string.Empty;
        public string AdminNotes         { get; set; } = string.Empty;
    }
}
