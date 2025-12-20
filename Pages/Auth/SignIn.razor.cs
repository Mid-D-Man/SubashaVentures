// Pages/Auth/SignIn.razor.cs - FIXED AUTH FLOW
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Services.Storage;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Auth;

public partial class SignIn : ComponentBase
{
    [Inject] private ISupabaseAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<SignIn> Logger { get; set; } = default!;
    [Inject] private IBlazorAppLocalStorageService LocalStorage { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "registered")]
    private bool Registered { get; set; }

    // ✅ NEW: Return URL parameter for redirect after sign-in
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
        // ✅ CRITICAL FIX: Check if user is already authenticated
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        if (authState.User?.Identity?.IsAuthenticated ?? false)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "User already authenticated, redirecting...",
                LogLevel.Info
            );

            // Redirect to return URL or home
            var destination = !string.IsNullOrEmpty(ReturnUrl) ? ReturnUrl : "/";
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

        // ✅ NEW: Decode return URL if present
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

                // ✅ CRITICAL: Notify authentication state changed BEFORE navigation
                if (AuthStateProvider is SupabaseAuthStateProvider provider)
                {
                    provider.NotifyAuthenticationStateChanged();
                    
                    // Wait for authentication state to update
                    await Task.Delay(500);
                }

                // ✅ FIXED: Navigate to return URL or home (NO forceLoad)
                var destination = !string.IsNullOrEmpty(ReturnUrl) ? ReturnUrl : "/";
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Redirecting to: {destination}",
                    LogLevel.Info
                );

                NavigationManager.NavigateTo(destination, forceLoad: false);
            }
            else
            {
                if (result.ErrorCode == "INVALID_CREDENTIALS")
                {
                    generalError = "Invalid email or password. Please try again.";
                }
                else if (result.ErrorCode == "EMAIL_NOT_CONFIRMED")
                {
                    generalError = "Please verify your email before signing in.";
                }
                else
                {
                    generalError = result.Message ?? "Sign in failed. Please try again.";
                }
                
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

    private async Task HandleGoogleSignIn()
    {
        try
        {
            isLoading = true;
            ClearErrors();
            successMessage = "";
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                "Initiating Google OAuth sign in",
                LogLevel.Info
            );
            
            // ✅ NEW: Store return URL before OAuth redirect
            if (!string.IsNullOrEmpty(ReturnUrl))
            {
                await LocalStorage.SetItemAsync("oauth_return_url", ReturnUrl);
            }
            
            var success = await AuthService.SignInWithGoogleAsync();
            
            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✓ Google OAuth initiated successfully",
                    LogLevel.Info
                );

                Logger.LogInformation("Google OAuth sign in initiated");
                
                // The OAuth flow will redirect to Google automatically
                // When user returns, they'll be authenticated
            }
            else
            {
                generalError = "Failed to connect to Google. Please try again.";
                Logger.LogError("Google OAuth initiation failed");
                isLoading = false;
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            generalError = "Failed to sign in with Google. Please try again.";
            await MID_HelperFunctions.LogExceptionAsync(ex, "Google OAuth sign in");
            Logger.LogError(ex, "Google sign in error");
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
