// Pages/Auth/SignIn.razor.cs - UPDATED FOR C# AUTH
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Services.Storage;
using SubashaVentures.Services.Auth;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Auth;

public partial class SignIn : ComponentBase
{
    [Inject] private SupabaseAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<SignIn> Logger { get; set; } = default!;
    [Inject] private IBlazorAppLocalStorageService LocalStorage { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "registered")]
    private bool Registered { get; set; }

    [SupplyParameterFromQuery(Name = "returnUrl")]
    private string? ReturnUrl { get; set; }

    private string email = "";
    private string password = "";
    private bool rememberMe = false;
    private bool showPassword = false;
    
    private string emailError = "";
    private string passwordError = "";
    private string generalError = "";
    private string successMessage = "";
    
    private bool isLoading = false;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        if (authState.User?.Identity?.IsAuthenticated ?? false)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "User already authenticated, redirecting...",
                LogLevel.Info
            );

            var destination = !string.IsNullOrEmpty(ReturnUrl) ? ReturnUrl : "";
            NavigationManager.NavigateTo(destination, forceLoad: false);
            return;
        }

        if (Registered)
        {
            successMessage = "Account created successfully! Please sign in.";
        }

        try
        {
            var rememberedEmail = await LocalStorage.GetItemAsync<string>("remember_email");
            if (!string.IsNullOrEmpty(rememberedEmail))
            {
                email = rememberedEmail;
                rememberMe = true;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load remembered email");
        }

        if (!string.IsNullOrEmpty(ReturnUrl))
        {
            try
            {
                ReturnUrl = Uri.UnescapeDataString(ReturnUrl);
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Will redirect to: {ReturnUrl} after sign-in",
                    LogLevel.Info
                );
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to decode return URL");
                ReturnUrl = null;
            }
        }
    }

    private async Task HandleSignIn()
    {
        ClearErrors();
        successMessage = "";
        
        if (!ValidateForm())
        {
            return;
        }
        
        isLoading = true;
        StateHasChanged();
        
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Attempting sign in for: {email}",
                LogLevel.Info
            );

            var result = await AuthService.SignInAsync(email, password);
            
            if (result.Success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ User signed in successfully: {email}",
                    LogLevel.Info
                );

                Logger.LogInformation("User signed in successfully: {Email}", email);
                
                if (rememberMe)
                {
                    await LocalStorage.SetItemAsync("remember_email", email);
                }
                else
                {
                    await LocalStorage.RemoveItemAsync("remember_email");
                }

                if (AuthStateProvider is SupabaseAuthStateProvider provider)
                {
                    provider.NotifyAuthenticationStateChanged();
                    await Task.Delay(500);
                }

                var destination = !string.IsNullOrEmpty(ReturnUrl) ? ReturnUrl : "";
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Redirecting to: {destination}",
                    LogLevel.Info
                );

                NavigationManager.NavigateTo(destination, forceLoad: false);
            }
            else
            {
                generalError = result.Message;
                Logger.LogWarning("Sign in failed for {Email}: {Message}", email, result.Message);
            }
        }
        catch (Exception ex)
        {
            generalError = "An error occurred during sign in. Please try again later.";
            await MID_HelperFunctions.LogExceptionAsync(ex, "Sign in");
            Logger.LogError(ex, "Error during sign in for {Email}", email);
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private bool ValidateForm()
    {
        bool isValid = true;
        
        if (string.IsNullOrWhiteSpace(email))
        {
            emailError = "Email is required";
            isValid = false;
        }
        else if (!IsValidEmail(email))
        {
            emailError = "Please enter a valid email address";
            isValid = false;
        }
        
        if (string.IsNullOrWhiteSpace(password))
        {
            passwordError = "Password is required";
            isValid = false;
        }
        else if (password.Length < 6)
        {
            passwordError = "Password must be at least 6 characters";
            isValid = false;
        }
        
        return isValid;
    }

    private void ClearErrors()
    {
        emailError = "";
        passwordError = "";
        generalError = "";
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
