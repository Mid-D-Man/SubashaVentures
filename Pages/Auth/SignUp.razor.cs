// Pages/Auth/SignUp.razor.cs
using Microsoft.AspNetCore.Components;
using SubashaVentures.Services.Auth;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.Users;
using SubashaVentures.Services.Storage;
using SubashaVentures.Services.VisualElements;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Auth;

public partial class SignUp : ComponentBase
{
    [Inject] private SupabaseAuthService           AuthService       { get; set; } = default!;
    [Inject] private NavigationManager             NavigationManager { get; set; } = default!;
    [Inject] private ILogger<SignUp>               Logger            { get; set; } = default!;
    [Inject] private AuthenticationStateProvider   AuthStateProvider { get; set; } = default!;
    [Inject] private IUserService                  UserService       { get; set; } = default!;
    [Inject] private IBlazorAppLocalStorageService LocalStorage      { get; set; } = default!;
    [Inject] private IVisualElementsService        VisualElements    { get; set; } = default!;

    // ── Form fields ────────────────────────────────────────────────────────
    private string firstName       = "";
    private string lastName        = "";
    private string email           = "";
    private string password        = "";
    private string confirmPassword = "";
    private bool   acceptTerms     = false;
    private bool   showPassword        = false;
    private bool   showConfirmPassword = false;

    // ── Errors ────────────────────────────────────────────────────────────
    private string firstNameError       = "";
    private string lastNameError        = "";
    private string emailError           = "";
    private string passwordError        = "";
    private string confirmPasswordError = "";
    private string termsError           = "";
    private string generalError         = "";
    private string successMessage       = "";
    private bool   isLoading            = false;

    // ── SVG icons ──────────────────────────────────────────────────────────
    private string _mailIcon    = string.Empty;
    private string _lockIcon    = string.Empty;
    private string _checkIcon   = string.Empty;
    private string _warningIcon = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        // Check if already authenticated
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        if (authState.User?.Identity?.IsAuthenticated ?? false)
        {
            var destination = await DetermineRedirectDestinationAsync();
            NavigationManager.NavigateTo(destination, forceLoad: false);
            return;
        }

        await LoadIconsAsync();
    }

    private async Task LoadIconsAsync()
    {
        try
        {
            _mailIcon = await VisualElements.GetCustomSvgAsync(
                SvgType.Mail, width: 18, height: 18, fillColor: "currentColor");

            _checkIcon = await VisualElements.GetCustomSvgAsync(
                SvgType.CheckMark, width: 16, height: 16, fillColor: "currentColor");

            _warningIcon = await VisualElements.GetCustomSvgAsync(
                SvgType.Warning, width: 16, height: 16, fillColor: "currentColor");

            _lockIcon = VisualElements.GenerateSvg(
                "<rect x='3' y='11' width='18' height='11' rx='2' ry='2' stroke='currentColor' stroke-width='1.5' fill='none'/>" +
                "<path stroke='currentColor' stroke-width='1.5' stroke-linecap='round' fill='none' d='M7 11V7a5 5 0 0 1 10 0v4'/>",
                18, 18, "0 0 24 24");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SignUp icon load error: {ex.Message}");
        }
    }

    // ── Email/password sign-up ─────────────────────────────────────────────

    private async Task HandleSignUp()
    {
        ClearErrors();
        successMessage = "";

        if (!ValidateForm()) return;

        isLoading = true;
        StateHasChanged();

        try
        {
            var userData = new UserModel
            {
                FirstName           = firstName.Trim(),
                LastName            = lastName.Trim(),
                Email               = email.Trim().ToLowerInvariant(),
                EmailNotifications  = true,
                SmsNotifications    = false,
                PreferredLanguage   = "en",
                Currency            = "NGN",
                AccountStatus       = "Active",
                MembershipTier      = "Bronze",
                CreatedAt           = DateTime.UtcNow,
                CreatedBy           = "system"
            };

            var result = await AuthService.SignUpAsync(email, password, userData);

            if (result.Success)
            {
                successMessage = result.Message;

                if (AuthStateProvider is SupabaseAuthStateProvider provider)
                    provider.NotifyAuthenticationStateChanged();

                await Task.Delay(3000);
                NavigationManager.NavigateTo("signin?registered=true", forceLoad: false);
            }
            else
            {
                if (result.ErrorCode == "user_already_exists")
                    emailError = result.Message;
                else
                    generalError = result.Message;
            }
        }
        catch (Exception ex)
        {
            generalError = "An error occurred during registration. Please try again.";
            Logger.LogError(ex, "Error during sign up for {Email}", email);
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    // ── Google OAuth ───────────────────────────────────────────────────────

    private async Task HandleGoogleSignUp()
    {
        try
        {
            ClearErrors();
            isLoading = true;
            StateHasChanged();

            var success = await AuthService.SignInWithGoogleAsync(null);

            if (!success)
            {
                generalError = "Failed to initiate Google sign-up. Please try again.";
                isLoading    = false;
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            generalError = "An error occurred with Google sign-up. Please try again.";
            Logger.LogError(ex, "Error during Google sign-up");
            isLoading = false;
            StateHasChanged();
        }
    }

    // ── Role-based redirect ────────────────────────────────────────────────

    private async Task<string> DetermineRedirectDestinationAsync()
    {
        try
        {
            var user = await AuthService.GetCurrentUserAsync();
            if (user == null) return "";

            var profile = await UserService.GetUserByIdAsync(user.Id);
            return profile?.IsSuperiorAdmin == true ? "admin" : "";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error determining redirect destination");
            return "";
        }
    }

    // ── Validation ─────────────────────────────────────────────────────────

    private bool ValidateForm()
    {
        bool valid = true;

        if (string.IsNullOrWhiteSpace(firstName) || firstName.Trim().Length < 2)
        { firstNameError = "First name must be at least 2 characters"; valid = false; }
        else if (!IsValidName(firstName))
        { firstNameError = "First name contains invalid characters"; valid = false; }

        if (string.IsNullOrWhiteSpace(lastName) || lastName.Trim().Length < 2)
        { lastNameError = "Last name must be at least 2 characters"; valid = false; }
        else if (!IsValidName(lastName))
        { lastNameError = "Last name contains invalid characters"; valid = false; }

        if (string.IsNullOrWhiteSpace(email))
        { emailError = "Email is required"; valid = false; }
        else if (!IsValidEmail(email))
        { emailError = "Please enter a valid email address"; valid = false; }

        if (string.IsNullOrWhiteSpace(password))
        { passwordError = "Password is required"; valid = false; }
        else if (password.Length < 8)
        { passwordError = "Password must be at least 8 characters"; valid = false; }
        else if (!IsStrongPassword(password))
        { passwordError = "Password must contain uppercase, lowercase, and a number"; valid = false; }

        if (string.IsNullOrWhiteSpace(confirmPassword))
        { confirmPasswordError = "Please confirm your password"; valid = false; }
        else if (password != confirmPassword)
        { confirmPasswordError = "Passwords do not match"; valid = false; }

        if (!acceptTerms)
        { termsError = "You must accept the terms and conditions"; valid = false; }

        return valid;
    }

    private void ClearErrors()
    {
        firstNameError = lastNameError = emailError = passwordError =
            confirmPasswordError = termsError = generalError = "";
    }

    private static bool IsValidEmail(string email)
    {
        try   { return new System.Net.Mail.MailAddress(email).Address == email; }
        catch { return false; }
    }

    private static bool IsValidName(string name) =>
        name.Trim().All(c => char.IsLetter(c) || c == ' ' || c == '-' || c == '\'');

    private static bool IsStrongPassword(string password) =>
        password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit);
}
