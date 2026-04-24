using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Domain.Partner;
using SubashaVentures.Services.Partners;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Components.Shared.Notifications;

namespace SubashaVentures.Pages.Admin;

public partial class PartnerPayoutManagement : ComponentBase
{
    [Inject] private IPartnerStoreService        StoreService      { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private NavigationManager           Navigation        { get; set; } = default!;

    // ── State ──────────────────────────────────────────────────
    private bool isLoading         = true;
    private bool isPaidModalOpen    = false;
    private bool isRejectPayoutOpen = false;
    private bool isActioning        = false;

    private string adminUserId    = string.Empty;
    private string searchQuery    = string.Empty;
    private string selectedStatus = string.Empty;
    private string sortBy         = "newest";

    // ── Stats ──────────────────────────────────────────────────
    private int     pendingCount    = 0;
    private int     processingCount = 0;
    private int     paidCount       = 0;
    private decimal pendingAmount   = 0;

    // ── Data ───────────────────────────────────────────────────
    private List<PartnerPayoutRequestViewModel> all      = new();
    private List<PartnerPayoutRequestViewModel> filtered = new();
    private PartnerPayoutRequestViewModel?      selectedPayout = null;

    // ── Forms ──────────────────────────────────────────────────
    private MarkPaidFormData      paidForm            = new();
    private RejectPayoutFormData  rejectPayoutForm    = new();
    private Dictionary<string, string> paidErrors         = new();
    private Dictionary<string, string> rejectPayoutErrors = new();

    // ── Refs ───────────────────────────────────────────────────
    private DynamicModal?          paidModal;
    private DynamicModal?          rejectPayoutModal;
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
            Console.WriteLine($"PartnerPayoutManagement init: {ex.Message}");
            isLoading = false;
        }
    }

    private async Task LoadDataAsync()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            all = await StoreService.GetAllPayoutRequestsAsync();

            pendingCount    = all.Count(p => p.IsPending);
            processingCount = all.Count(p => p.IsProcessing);
            paidCount       = all.Count(p => p.IsPaid);
            pendingAmount   = all.Where(p => p.IsPending || p.IsProcessing)
                .Sum(p => p.AmountRequested);

            ApplyFilter();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PartnerPayoutManagement load: {ex.Message}");
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
        var q = all.Where(p =>
            (string.IsNullOrEmpty(searchQuery) ||
             p.BankAccountName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
             p.BankName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)         ||
             p.PartnerId.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(selectedStatus) || p.Status == selectedStatus));

        filtered = sortBy switch
        {
            "oldest"       => q.OrderBy(p => p.RequestedAt).ToList(),
            "amount-high"  => q.OrderByDescending(p => p.AmountRequested).ToList(),
            "amount-low"   => q.OrderBy(p => p.AmountRequested).ToList(),
            _              => q.OrderByDescending(p => p.RequestedAt).ToList()
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

    // ── Mark Processing ────────────────────────────────────────

    private async Task OpenMarkProcessingModal(PartnerPayoutRequestViewModel payout)
    {
        isActioning = true;
        StateHasChanged();

        try
        {
            var success = await StoreService.MarkPayoutProcessingAsync(
                payout.Id, adminUserId);

            if (success)
            {
                notificationComponent?.ShowSuccess("Payout marked as Processing.");
                await LoadDataAsync();
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

    // ── Mark Paid ──────────────────────────────────────────────

    private void OpenMarkPaidModal(PartnerPayoutRequestViewModel payout)
    {
        selectedPayout = payout;
        paidForm       = new MarkPaidFormData();
        paidErrors.Clear();
        isPaidModalOpen = true;
    }

    private void ClosePaidModal()
    {
        isPaidModalOpen = false;
        selectedPayout  = null;
        paidForm        = new MarkPaidFormData();
        paidErrors.Clear();
    }

    private async Task HandleMarkPaid()
    {
        paidErrors.Clear();

        if (string.IsNullOrWhiteSpace(paidForm.TransactionReference))
            paidErrors["Reference"] = "Transaction reference is required";

        if (paidErrors.Any())
        {
            StateHasChanged();
            return;
        }

        if (selectedPayout == null) return;

        isActioning = true;
        StateHasChanged();

        try
        {
            var request = new MarkPayoutPaidRequest
            {
                PayoutRequestId      = selectedPayout.Id,
                AdminUserId          = adminUserId,
                TransactionReference = paidForm.TransactionReference.Trim(),
                PaymentMethod        = paidForm.PaymentMethod,
                AdminNotes           = string.IsNullOrWhiteSpace(paidForm.AdminNotes)
                    ? null : paidForm.AdminNotes.Trim()
            };

            var success = await StoreService.MarkPayoutPaidAsync(request);

            if (success)
            {
                notificationComponent?.ShowSuccess(
                    $"Payout of {selectedPayout.DisplayAmount} marked as Paid.");
                ClosePaidModal();
                await LoadDataAsync();
            }
            else
            {
                notificationComponent?.ShowError("Failed to mark as paid.");
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

    // ── Reject Payout ──────────────────────────────────────────

    private void OpenRejectPayoutModal(PartnerPayoutRequestViewModel payout)
    {
        selectedPayout    = payout;
        rejectPayoutForm  = new RejectPayoutFormData();
        rejectPayoutErrors.Clear();
        isRejectPayoutOpen = true;
    }

    private void CloseRejectPayoutModal()
    {
        isRejectPayoutOpen = false;
        selectedPayout     = null;
        rejectPayoutForm   = new RejectPayoutFormData();
        rejectPayoutErrors.Clear();
    }

    private async Task HandleRejectPayout()
    {
        rejectPayoutErrors.Clear();

        if (string.IsNullOrWhiteSpace(rejectPayoutForm.Reason) ||
            rejectPayoutForm.Reason.Trim().Length < 5)
            rejectPayoutErrors["Reason"] = "Please provide a reason";

        if (rejectPayoutErrors.Any())
        {
            StateHasChanged();
            return;
        }

        if (selectedPayout == null) return;

        isActioning = true;
        StateHasChanged();

        try
        {
            var success = await StoreService.RejectPayoutRequestAsync(
                selectedPayout.Id,
                adminUserId,
                rejectPayoutForm.Reason.Trim());

            if (success)
            {
                notificationComponent?.ShowSuccess("Payout request rejected.");
                CloseRejectPayoutModal();
                await LoadDataAsync();
            }
            else
            {
                notificationComponent?.ShowError("Failed to reject payout.");
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

    private bool   HasPaidError(string f)         => paidErrors.ContainsKey(f);
    private string GetPaidError(string f)         => paidErrors.GetValueOrDefault(f, string.Empty);
    private bool   HasRejectPayoutError(string f) => rejectPayoutErrors.ContainsKey(f);
    private string GetRejectPayoutError(string f) => rejectPayoutErrors.GetValueOrDefault(f, string.Empty);

    // ── Form models ────────────────────────────────────────────

    public class MarkPaidFormData
    {
        public string TransactionReference { get; set; } = string.Empty;
        public string PaymentMethod        { get; set; } = "bank_transfer";
        public string AdminNotes           { get; set; } = string.Empty;
    }

    public class RejectPayoutFormData
    {
        public string Reason { get; set; } = string.Empty;
    }
}
