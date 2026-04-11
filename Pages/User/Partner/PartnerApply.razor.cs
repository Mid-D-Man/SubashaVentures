using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Domain.Partner;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Services.Partners;

namespace SubashaVentures.Pages.User.Partner;

public partial class PartnerApply : ComponentBase
{
    [Inject] private IPartnerApplicationService PartnerApplicationService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private bool isLoading = true;
    private bool isSubmitting = false;
    private int currentStep = 1;
    private string userId = string.Empty;
    private string submitError = string.Empty;

    private ApplicationEligibilityResult eligibility = new() { IsEligible = false };
    private PartnerApplicationViewModel? existingApplication = null;

    private Dictionary<string, string> validationErrors = new();

    private ApplyFormData form = new();

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

            userId = user.FindFirst("sub")?.Value
                  ?? user.FindFirst("id")?.Value
                  ?? string.Empty;

            if (string.IsNullOrEmpty(userId))
            {
                Navigation.NavigateTo("signin");
                return;
            }

            // Pre-fill email from auth claims
            form.Email = user.Identity.Name ?? string.Empty;

            // Check eligibility and existing application in parallel
            var eligibilityTask = PartnerApplicationService.CheckEligibilityAsync(userId);
            var existingTask    = PartnerApplicationService.GetUserApplicationAsync(userId);

            await Task.WhenAll(eligibilityTask, existingTask);

            eligibility          = await eligibilityTask;
            existingApplication  = await existingTask;

            // If they have an active or rejected (in cooldown) application show that instead
            if (existingApplication != null &&
                (existingApplication.Status == PartnerApplicationStatus.Pending   ||
                 existingApplication.Status == PartnerApplicationStatus.UnderReview ||
                 existingApplication.IsInCooldown ||
                 existingApplication.IsPermanentlyRejected))
            {
                // existingApplication view will be shown
            }
            else if (existingApplication?.Status == PartnerApplicationStatus.Approved)
            {
                // Already a partner — redirect to dashboard
                Navigation.NavigateTo("user/partner/dashboard");
                return;
            }
            else if (existingApplication?.CanReapply == true)
            {
                // Can reapply — clear existing so the form shows
                existingApplication = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PartnerApply init error: {ex.Message}");
        }
        finally
        {
            isLoading = false;
        }
    }

    private void GoToStep2()
    {
        validationErrors.Clear();

        if (string.IsNullOrWhiteSpace(form.FullName) || form.FullName.Trim().Length < 2)
            validationErrors["FullName"] = "Full name must be at least 2 characters";

        if (string.IsNullOrWhiteSpace(form.Phone) || form.Phone.Trim().Length < 10)
            validationErrors["Phone"] = "Enter a valid phone number";

        if (string.IsNullOrWhiteSpace(form.Email) || !form.Email.Contains('@'))
            validationErrors["Email"] = "Enter a valid email address";

        if (string.IsNullOrWhiteSpace(form.BusinessName) || form.BusinessName.Trim().Length < 2)
            validationErrors["BusinessName"] = "Business name is required";

        if (string.IsNullOrWhiteSpace(form.BusinessType))
            validationErrors["BusinessType"] = "Select a business type";

        if (string.IsNullOrWhiteSpace(form.Location))
            validationErrors["Location"] = "Select your location";

        if (string.IsNullOrWhiteSpace(form.Reason) || form.Reason.Trim().Length < 50)
            validationErrors["Reason"] = "Please provide at least 50 characters";

        if (validationErrors.Any()) return;

        currentStep = 2;
    }

    private void GoToStep3()
    {
        validationErrors.Clear();

        if (string.IsNullOrWhiteSpace(form.BankAccountName) || form.BankAccountName.Trim().Length < 2)
            validationErrors["BankAccountName"] = "Account name is required";

        if (string.IsNullOrWhiteSpace(form.BankAccountNumber) ||
            !form.BankAccountNumber.All(char.IsDigit) ||
            form.BankAccountNumber.Length is < 10 or > 12)
            validationErrors["BankAccountNumber"] = "Enter a valid 10-12 digit account number";

        if (string.IsNullOrWhiteSpace(form.BankName))
            validationErrors["BankName"] = "Bank name is required";

        if (validationErrors.Any()) return;

        currentStep = 3;
    }

    private async Task HandleSubmit()
    {
        submitError = string.Empty;
        isSubmitting = true;
        StateHasChanged();

        try
        {
            var request = new SubmitPartnerApplicationRequest
            {
                FullName          = form.FullName.Trim(),
                BusinessName      = form.BusinessName.Trim(),
                BusinessType      = form.BusinessType,
                Location          = form.Location,
                Phone             = form.Phone.Trim(),
                Email             = form.Email.Trim(),
                Reason            = form.Reason.Trim(),
                BankAccountName   = form.BankAccountName.Trim(),
                BankAccountNumber = form.BankAccountNumber.Trim(),
                BankName          = form.BankName.Trim(),
                BankCode          = string.IsNullOrWhiteSpace(form.BankCode) ? null : form.BankCode.Trim(),
            };

            var result = await PartnerApplicationService.SubmitApplicationAsync(userId, request);

            if (result != null)
            {
                currentStep = 4;
            }
            else
            {
                submitError = "Failed to submit application. Please check your details and try again.";
            }
        }
        catch (Exception ex)
        {
            submitError = $"An error occurred: {ex.Message}";
        }
        finally
        {
            isSubmitting = false;
            StateHasChanged();
        }
    }

    private bool HasError(string field) =>
        validationErrors.ContainsKey(field);

    private string GetError(string field) =>
        validationErrors.GetValueOrDefault(field, string.Empty);

    private string MaskAccountNumber(string number)
    {
        if (number.Length < 4) return number;
        return new string('*', number.Length - 4) + number[^4..];
    }

    public class ApplyFormData
    {
        public string FullName          { get; set; } = string.Empty;
        public string BusinessName      { get; set; } = string.Empty;
        public string BusinessType      { get; set; } = string.Empty;
        public string Location          { get; set; } = string.Empty;
        public string Phone             { get; set; } = string.Empty;
        public string Email             { get; set; } = string.Empty;
        public string Reason            { get; set; } = string.Empty;
        public string BankAccountName   { get; set; } = string.Empty;
        public string BankAccountNumber { get; set; } = string.Empty;
        public string BankName          { get; set; } = string.Empty;
        public string BankCode          { get; set; } = string.Empty;
    }
}
