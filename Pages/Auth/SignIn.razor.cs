// Pages/Auth/SignIn.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Services.Storage;
using SubashaVentures.Services.Auth;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.Users;
using SubashaVentures.Services.VisualElements;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Auth;

public partial class SignIn : ComponentBase
{
    [Inject] private SupabaseAuthService         AuthService       { get; set; } = default!;
    [Inject] private NavigationManager           NavigationManager { get; set; } = default!;
    [Inject] private ILogger<SignIn>             Logger            { get; set; } = default!;
    [Inject] private IBlazorAppLocalStorageService LocalStorage    { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private IUserService                UserService       { get; set; } = default!;
    [Inject] private IVisualElementsService      VisualElements    { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "registered")]
    private bool Registered { get; set; }

    [SupplyParameterFromQuery(Name = "returnUrl")]
    private string? ReturnUrl { get; set; }

    // ── Form state ─────────────────────────────────────────────────────────
    private string email         = "";
    private string password      = "";
    private bool   rememberMe    = false;
    private bool   showPassword  = false;

    private string emailError    = "";
    private string passwordError = "";
    private string generalError  = "";
    private string successMessage = "";
    private bool   isLoading     = false;

    // ── SVG icons ──────────────────────────────────────────────────────────
    private string _mailIcon    = string.Empty;
    private string _lockIcon    = string.Empty;
    private string _checkIcon   = string.Empty;
    private string _warningIcon = string.Empty;
    private string _infoIcon    = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(LoadIconsAsync(), LoadPrefsAsync());

        if (Registered)
            successMessage = "Account created successfully! Please sign in.";

        if (!string.IsNullOrEmpty(ReturnUrl))
        {
            try   { ReturnUrl = Uri.UnescapeDataString(ReturnUrl); }
            catch { ReturnUrl = null; }
        }
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

            // Lock — inline generated (no SvgType entry)
            _lockIcon = VisualElements.GenerateSvg(
                "<rect x='3' y='11' width='18' height='11' rx='2' ry='2' stroke='currentColor' stroke-width='1.5' fill='none'/>" +
                "<path stroke='currentColor' stroke-width='1.5' stroke-linecap='round' fill='none' d='M7 11V7a5 5 0 0 1 10 0v4'/>",
                18, 18, "0 0 24 24");

            // Info circle — inline generated
            _infoIcon = VisualElements.GenerateSvg(
                "<circle cx='12' cy='12' r='10' stroke='currentColor' stroke-width='1.5' fill='none'/>" +
                "<path stroke='currentColor' stroke-width='1.5' stroke-linecap='round' fill='none' d='M12 16v-4M12 8h.01'/>",
                16, 16, "0 0 24 24");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SignIn icon load error: {ex.Message}");
        }
    }

    private async Task LoadPrefsAsync()
    {
        try
        {
            var rememberedEmail = await LocalStorage.GetItemAsync<string>("remember_email");
            if (!string.IsNullOrEmpty(rememberedEmail))
            {
                email      = rememberedEmail;
                rememberMe = true;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load remembered email");
        }
    }

    // ── Email/password sign-in ─────────────────────────────────────────────

    private async Task HandleSignIn()
    {
        ClearErrors();
        successMessage = "";

        if (!ValidateForm()) return;

        isLoading = true;
        StateHasChanged();

        try
        {
            var result = await AuthService.SignInAsync(email, password);

            if (result.Success)
            {
                if (rememberMe)
                    await LocalStorage.SetItemAsync("remember_email", email);
                else
                    await LocalStorage.RemoveItemAsync("remember_email");

                if (AuthStateProvider is SupabaseAuthStateProvider provider)
                {
                    provider.NotifyAuthenticationStateChanged();
                    await Task.Delay(500);
                }

                var destination = await DetermineRedirectDestinationAsync();
                NavigationManager.NavigateTo(destination, forceLoad: false);
            }
            else
            {
                generalError = result.Message;
            }
        }
        catch (Exception ex)
        {
            generalError = "An error occurred during sign in. Please try again.";
            Logger.LogError(ex, "Error during sign in for {Email}", email);
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    // ── Google OAuth ───────────────────────────────────────────────────────

    private async Task HandleGoogleSignIn()
    {
        try
        {
            ClearErrors();
            isLoading = true;
            StateHasChanged();

            var returnUrl = !string.IsNullOrEmpty(ReturnUrl) ? ReturnUrl : null;
            var success   = await AuthService.SignInWithGoogleAsync(returnUrl);

            if (!success)
            {
                generalError = "Failed to initiate Google sign-in. Please try again.";
                isLoading    = false;
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            generalError = "An error occurred with Google sign-in. Please try again.";
            Logger.LogError(ex, "Error during Google sign-in");
            isLoading = false;
            StateHasChanged();
        }
    }

    // ── Role-based redirect ────────────────────────────────────────────────

    private async Task<string> DetermineRedirectDestinationAsync()
    {
        try
        {
            if (!string.IsNullOrEmpty(ReturnUrl)) return ReturnUrl;

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

        if (string.IsNullOrWhiteSpace(email))
        { emailError = "Email is required"; valid = false; }
        else if (!IsValidEmail(email))
        { emailError = "Please enter a valid email address"; valid = false; }

        if (string.IsNullOrWhiteSpace(password))
        { passwordError = "Password is required"; valid = false; }
        else if (password.Length < 6)
        { passwordError = "Password must be at least 6 characters"; valid = false; }

        return valid;
    }

    private void ClearErrors()
    {
        emailError = passwordError = generalError = "";
    }

    private static bool IsValidEmail(string email)
    {
        try   { return new System.Net.Mail.MailAddress(email).Address == email; }
        catch { return false; }
    }
}
