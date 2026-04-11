using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Domain.Partner;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Services.Partners;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Components.Shared.Notifications;

namespace SubashaVentures.Pages.Admin;

public partial class PartnerApplications : ComponentBase
{
    [Inject] private IPartnerApplicationService PartnerApplicationService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider         { get; set; } = default!;
    [Inject] private NavigationManager Navigation                          { get; set; } = default!;

    // ── State ──────────────────────────────────────────────────
    private bool isLoading       = true;
    private bool isDetailOpen    = false;
    private bool isContactLogOpen = false;
    private bool isRejectOpen    = false;
    private bool isActioning     = false;
    private bool isSavingLog     = false;

    private string adminUserId   = string.Empty;
    private string adminName     = string.Empty;
    private string searchQuery   = string.Empty;
    private string selectedStatus   = string.Empty;
    private string selectedLocation = string.Empty;
    private string sortBy           = "newest";

    // ── Data ───────────────────────────────────────────────────
    private List<PartnerApplicationViewModel> all      = new();
    private List<PartnerApplicationViewModel> filtered = new();
    private ApplicationStatistics stats = new();

    private PartnerApplicationViewModel? selectedApplication = null;

    // ── Forms ──────────────────────────────────────────────────
    private ContactLogFormData logForm    = new();
    private RejectFormData     rejectForm = new();
    private DateTime           logFormDate = DateTime.Today;

    private Dictionary<string, string> logErrors    = new();
    private Dictionary<string, string> rejectErrors = new();

    // ── Component refs ─────────────────────────────────────────
    private DynamicModal?          detailModal;
    private DynamicModal?          contactLogModal;
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

            var firstName = user.FindFirst("first_name")?.Value ?? "";
            var lastName  = user.FindFirst("last_name")?.Value  ?? "";
            adminName     = $"{firstName} {lastName}".Trim();
            if (string.IsNullOrEmpty(adminName)) adminName = "Admin";

            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PartnerApplications init error: {ex.Message}");
            isLoading = false;
        }
    }

    private async Task LoadDataAsync()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            var allTask   = PartnerApplicationService.GetAllApplicationsAsync();
            var statsTask = PartnerApplicationService.GetApplicationStatisticsAsync();

            await Task.WhenAll(allTask, statsTask);

            all   = await allTask;
            stats = await statsTask;

            ApplyFilter();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PartnerApplications load error: {ex.Message}");
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
        var q = filtered = all
            .Where(a =>
                (string.IsNullOrEmpty(searchQuery) ||
                 a.FullName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)     ||
                 a.Email.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)         ||
                 a.BusinessName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrEmpty(selectedStatus)   || a.Status == selectedStatus)   &&
                (string.IsNullOrEmpty(selectedLocation) || a.Location == selectedLocation))
            .ToList();

        filtered = sortBy == "oldest"
            ? q.OrderBy(a => a.SubmittedAt).ToList()
            : q.OrderByDescending(a => a.SubmittedAt).ToList();

        StateHasChanged();
    }

    private void HandleStatusFilter(ChangeEventArgs e)
    {
        selectedStatus = e.Value?.ToString() ?? string.Empty;
        ApplyFilter();
    }

    private void HandleLocationFilter(ChangeEventArgs e)
    {
        selectedLocation = e.Value?.ToString() ?? string.Empty;
        ApplyFilter();
    }

    private void HandleSortChange(ChangeEventArgs e)
    {
        sortBy = e.Value?.ToString() ?? "newest";
        ApplyFilter();
    }

    // ── Detail modal ───────────────────────────────────────────

    private void SelectApplication(PartnerApplicationViewModel app)
    {
        selectedApplication = app;
        isDetailOpen        = true;
        StateHasChanged();
    }

    private void CloseDetail()
    {
        isDetailOpen        = false;
        selectedApplication = null;
        StateHasChanged();
    }

    // ── Mark under review ──────────────────────────────────────

    private async Task HandleMarkUnderReview()
    {
        if (selectedApplication == null) return;

        isActioning = true;
        StateHasChanged();

        try
        {
            var success = await PartnerApplicationService.MarkUnderReviewAsync(
                selectedApplication.Id, adminUserId);

            if (success)
            {
                notificationComponent?.ShowSuccess("Application marked as Under Review.");
                await LoadDataAsync();
                CloseDetail();
            }
            else
            {
                notificationComponent?.ShowError("Failed to update status.");
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

    // ── Approve ────────────────────────────────────────────────

    private async Task HandleApprove()
    {
        if (selectedApplication == null) return;

        isActioning = true;
        StateHasChanged();

        try
        {
            var result = await PartnerApplicationService.ApproveApplicationAsync(
                selectedApplication.Id, adminUserId);

            if (result.Success)
            {
                notificationComponent?.ShowSuccess(
                    $"Application approved! Partner ID: {result.PartnerId}");
                await LoadDataAsync();
                CloseDetail();
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
        rejectForm  = new RejectFormData();
        rejectErrors.Clear();
        isRejectOpen = true;
    }

    private void CloseRejectModal()
    {
        isRejectOpen = false;
        rejectForm   = new RejectFormData();
        rejectErrors.Clear();
    }

    private async Task HandleConfirmReject()
    {
        rejectErrors.Clear();

        if (string.IsNullOrWhiteSpace(rejectForm.Reason) || rejectForm.Reason.Trim().Length < 10)
            rejectErrors["Reason"] = "Please provide a meaningful rejection reason (min 10 characters)";

        if (rejectErrors.Any())
        {
            StateHasChanged();
            return;
        }

        if (selectedApplication == null) return;

        isActioning = true;
        StateHasChanged();

        try
        {
            var success = await PartnerApplicationService.RejectApplicationAsync(
                selectedApplication.Id,
                adminUserId,
                rejectForm.Reason.Trim(),
                rejectForm.CooldownDaysOverride > 0 ? rejectForm.CooldownDaysOverride : null);

            if (success)
            {
                notificationComponent?.ShowSuccess("Application rejected.");
                CloseRejectModal();
                await LoadDataAsync();
                CloseDetail();
            }
            else
            {
                notificationComponent?.ShowError("Failed to reject application.");
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

    // ── Contact log ────────────────────────────────────────────

    private void OpenContactLogModal()
    {
        logForm     = new ContactLogFormData();
        logFormDate = DateTime.Today;
        logErrors.Clear();
        isContactLogOpen = true;
    }

    private void CloseContactLogModal()
    {
        isContactLogOpen = false;
        logForm          = new ContactLogFormData();
        logErrors.Clear();
    }

    private async Task HandleSaveContactLog()
    {
        logErrors.Clear();

        if (string.IsNullOrWhiteSpace(logForm.ContactMethod))
            logErrors["ContactMethod"] = "Select a contact method";

        if (string.IsNullOrWhiteSpace(logForm.Outcome))
            logErrors["Outcome"] = "Select an outcome";

        if (string.IsNullOrWhiteSpace(logForm.Notes) || logForm.Notes.Trim().Length < 5)
            logErrors["Notes"] = "Notes must be at least 5 characters";

        if (logErrors.Any())
        {
            StateHasChanged();
            return;
        }

        if (selectedApplication == null) return;

        isSavingLog = true;
        StateHasChanged();

        try
        {
            var request = new AddContactLogRequest
            {
                ApplicationId = selectedApplication.Id,
                AdminUserId   = adminUserId,
                AdminName     = adminName,
                ContactMethod = logForm.ContactMethod,
                ContactDate   = logFormDate.ToString("yyyy-MM-dd"),
                Notes         = logForm.Notes.Trim(),
                Outcome       = logForm.Outcome,
            };

            var success = await PartnerApplicationService.AddContactLogAsync(request);

            if (success)
            {
                notificationComponent?.ShowSuccess("Contact log added.");
                CloseContactLogModal();
                await LoadDataAsync();

                // Refresh selected application to show new log
                selectedApplication = all.FirstOrDefault(a => a.Id == selectedApplication.Id);
                StateHasChanged();
            }
            else
            {
                notificationComponent?.ShowError("Failed to save contact log.");
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error: {ex.Message}");
        }
        finally
        {
            isSavingLog = false;
            StateHasChanged();
        }
    }

    // ── Helpers ────────────────────────────────────────────────

    private string GetOutcomeCss(string outcome) => outcome switch
    {
        "positive"   => "outcome-positive",
        "negative"   => "outcome-negative",
        "neutral"    => "outcome-neutral",
        "no_response" => "outcome-no-response",
        _            => ""
    };

    private bool HasLogError(string f)    => logErrors.ContainsKey(f);
    private string GetLogError(string f)  => logErrors.GetValueOrDefault(f, string.Empty);
    private bool HasRejectError(string f)   => rejectErrors.ContainsKey(f);
    private string GetRejectError(string f) => rejectErrors.GetValueOrDefault(f, string.Empty);

    // ── Form models ────────────────────────────────────────────

    public class ContactLogFormData
    {
        public string ContactMethod { get; set; } = string.Empty;
        public string Outcome       { get; set; } = string.Empty;
        public string Notes         { get; set; } = string.Empty;
    }

    public class RejectFormData
    {
        public string Reason              { get; set; } = string.Empty;
        public int?   CooldownDaysOverride { get; set; }
    }
}
