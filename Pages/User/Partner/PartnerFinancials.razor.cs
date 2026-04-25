using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Domain.Partner;
using SubashaVentures.Services.Partners;
using SubashaVentures.Services.Users;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Components.Shared.Notifications;

namespace SubashaVentures.Pages.User.Partner;

public partial class PartnerFinancials : ComponentBase
{
    [Inject] private IPartnerStoreService        PartnerStoreService { get; set; } = default!;
    [Inject] private IUserService                UserService         { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider   { get; set; } = default!;
    [Inject] private NavigationManager           Navigation          { get; set; } = default!;

    private bool    isLoading              = true;
    private bool    isBankModalOpen        = false;
    private bool    isRequestingPayout     = false;
    private bool    isSubmittingBankUpdate = false;
    private string  userId                 = string.Empty;
    private string  partnerId              = string.Empty;
    private decimal payoutAmount           = 10000;

    private PartnerEarningsSummary?       summary   = null;
    private Dictionary<string, string>    bankErrors = new();
    private BankUpdateFormData            bankForm   = new();

    private DynamicModal?           bankModal;
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

            userId    = user.FindFirst("sub")?.Value ?? user.FindFirst("id")?.Value ?? string.Empty;
            partnerId = user.FindFirst("partner_id")?.Value ?? string.Empty;

            // ── DB fallback when JWT is stale ──────────────────────────────────
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
                    Console.WriteLine($"PartnerFinancials DB partnerId fallback: {ex.Message}");
                }
            }

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(partnerId))
            {
                Navigation.NavigateTo("user/partner/dashboard");
                return;
            }

            await LoadSummary();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PartnerFinancials init error: {ex.Message}");
            isLoading = false;
        }
    }

    private async Task LoadSummary()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            summary = await PartnerStoreService.GetEarningsSummaryAsync(partnerId);

            if (summary != null)
                payoutAmount = summary.PendingPayout;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PartnerFinancials load error: {ex.Message}");
            summary = null;
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task HandleRequestPayout()
    {
        if (summary == null) return;

        if (payoutAmount < 10000)
        {
            notificationComponent?.ShowWarning("Minimum payout amount is ₦10,000");
            return;
        }

        if (payoutAmount > summary.PendingPayout)
        {
            notificationComponent?.ShowWarning("Amount exceeds your available balance");
            return;
        }

        isRequestingPayout = true;
        StateHasChanged();

        try
        {
            var request = new RequestPayoutRequest
            {
                PartnerId       = partnerId,
                AmountRequested = payoutAmount,
            };

            var result = await PartnerStoreService.RequestPayoutAsync(partnerId, userId, request);

            if (result.Success)
            {
                notificationComponent?.ShowSuccess("Payout request submitted! Admin will process it shortly.");
                await LoadSummary();
            }
            else
            {
                notificationComponent?.ShowError(result.ErrorMessage ?? "Failed to submit payout request");
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error: {ex.Message}");
        }
        finally
        {
            isRequestingPayout = false;
            StateHasChanged();
        }
    }

    private void OpenBankUpdateModal()
    {
        bankForm = new BankUpdateFormData();
        bankErrors.Clear();
        isBankModalOpen = true;
    }

    private void CloseBankModal()
    {
        isBankModalOpen = false;
        bankForm        = new BankUpdateFormData();
        bankErrors.Clear();
    }

    private async Task HandleBankUpdate()
    {
        bankErrors.Clear();

        if (string.IsNullOrWhiteSpace(bankForm.NewAccountName) || bankForm.NewAccountName.Trim().Length < 2)
            bankErrors["NewAccountName"] = "Account name is required";

        if (string.IsNullOrWhiteSpace(bankForm.NewAccountNumber) ||
            !bankForm.NewAccountNumber.All(char.IsDigit) ||
            bankForm.NewAccountNumber.Length is < 10 or > 12)
            bankErrors["NewAccountNumber"] = "Enter a valid 10-12 digit account number";

        if (string.IsNullOrWhiteSpace(bankForm.NewBankName))
            bankErrors["NewBankName"] = "Bank name is required";

        if (string.IsNullOrWhiteSpace(bankForm.Reason) || bankForm.Reason.Trim().Length < 10)
            bankErrors["Reason"] = "Please provide a reason (minimum 10 characters)";

        if (bankErrors.Any()) { StateHasChanged(); return; }

        isSubmittingBankUpdate = true;
        StateHasChanged();

        try
        {
            var request = new RequestBankUpdateRequest
            {
                PartnerId        = partnerId,
                NewAccountName   = bankForm.NewAccountName.Trim(),
                NewAccountNumber = bankForm.NewAccountNumber.Trim(),
                NewBankName      = bankForm.NewBankName.Trim(),
                NewBankCode      = string.IsNullOrWhiteSpace(bankForm.NewBankCode) ? null : bankForm.NewBankCode.Trim(),
                Reason           = bankForm.Reason.Trim(),
            };

            var success = await PartnerStoreService.RequestBankUpdateAsync(partnerId, userId, request);

            if (success)
            {
                notificationComponent?.ShowSuccess("Bank update request submitted. Admin will review it shortly.");
                CloseBankModal();
                await LoadSummary();
            }
            else
            {
                notificationComponent?.ShowError("Failed to submit bank update request. You may already have a pending request.");
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error: {ex.Message}");
        }
        finally
        {
            isSubmittingBankUpdate = false;
            StateHasChanged();
        }
    }

    private bool   HasBankError(string field) => bankErrors.ContainsKey(field);
    private string GetBankError(string field) => bankErrors.GetValueOrDefault(field, string.Empty);

    public class BankUpdateFormData
    {
        public string NewAccountName   { get; set; } = string.Empty;
        public string NewAccountNumber { get; set; } = string.Empty;
        public string NewBankName      { get; set; } = string.Empty;
        public string NewBankCode      { get; set; } = string.Empty;
        public string Reason           { get; set; } = string.Empty;
    }
}
