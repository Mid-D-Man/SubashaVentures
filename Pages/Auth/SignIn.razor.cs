// Pages/Auth/SignIn.razor.cs - UPDATED with OAuth
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

    // Query parameter for success messages
    [SupplyParameterFromQuery(Name = "registered")]
    private bool Registered { get; set; }

    // Form fields
    private string email = "";
    private string password = "";
    private bool rememberMe = false;
    
    // Password visibility toggle
    private bool showPassword = false;
    
    // Error messages
    private string emailError = "";
    private string passwordError = "";
    private string generalError = "";
    private string successMessage = "";
    
    // Loading state
    private bool isLoading = false;

    protected override async Task OnInitializedAsync()
    {
        // Show success message if coming from registration
        if (Registered)
        {
            successMessage = "Account created successfully! Please sign in.";
        }

        // Check if there's a remembered email
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
    }

    // ==================== EMAIL/PASSWORD SIGN IN ====================

    private async Task HandleSignIn()
    {
        // Clear previous errors
        ClearErrors();
        successMessage = "";
        
        // Validate form
        if (!ValidateForm())
        {
            return;
        }
        
        // Set loading state
        isLoading = true;
        StateHasChanged();
        
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Attempting sign in for: {email}",
                LogLevel.Info
            );

            // Attempt sign in with Supabase
            var result = await AuthService.SignInAsync(email, password);
            
            if (result.Success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ User signed in successfully: {email}",
                    LogLevel.Info
                );

                Logger.LogInformation("User signed in successfully: {Email}", email);
                
                // Store remember me preference
                if (rememberMe)
                {
                    await LocalStorage.SetItemAsync("remember_email", email);
                }
                else
                {
                    await LocalStorage.RemoveItemAsync("remember_email");
                }

                // Notify auth state changed
                if (AuthStateProvider is SupabaseAuthStateProvider provider)
                {
                    provider.NotifyAuthenticationStateChanged();
                }
                
                // Navigate to home page
                NavigationManager.NavigateTo("/", forceLoad: true);
            }
            else
            {
                // Handle sign in failure
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

    // ==================== OAUTH SIGN IN ====================

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
            
            var success = await AuthService.SignInWithGoogleAsync();
            
            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✓ Google OAuth redirect initiated",
                    LogLevel.Info
                );

                Logger.LogInformation("Google OAuth sign in initiated");
                
                // Show loading message
                successMessage = "Redirecting to Google...";
                StateHasChanged();

                // The browser will redirect to Google OAuth page
                // When user returns, they'll be authenticated automatically
                // Supabase handles the callback
            }
            else
            {
                generalError = "Failed to connect to Google. Please try again.";
                Logger.LogError("Google OAuth initiation failed");
            }
        }
        catch (Exception ex)
        {
            generalError = "Failed to sign in with Google. Please try again.";
            await MID_HelperFunctions.LogExceptionAsync(ex, "Google OAuth sign in");
            Logger.LogError(ex, "Google sign in error");
        }
        finally
        {
            // Don't set loading to false here because we're redirecting
            // isLoading = false;
            StateHasChanged();
        }
    }

    private async Task HandleFacebookSignIn()
    {
        try
        {
            isLoading = true;
            ClearErrors();
            successMessage = "";
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                "Initiating Facebook OAuth sign in",
                LogLevel.Info
            );
            
            var success = await AuthService.SignInWithFacebookAsync();
            
            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✓ Facebook OAuth redirect initiated",
                    LogLevel.Info
                );

                Logger.LogInformation("Facebook OAuth sign in initiated");
                
                // Show loading message
                successMessage = "Redirecting to Facebook...";
                StateHasChanged();

                // The browser will redirect to Facebook OAuth page
            }
            else
            {
                generalError = "Failed to connect to Facebook. Please try again.";
                Logger.LogError("Facebook OAuth initiation failed");
            }
        }
        catch (Exception ex)
        {
            generalError = "Failed to sign in with Facebook. Please try again.";
            await MID_HelperFunctions.LogExceptionAsync(ex, "Facebook OAuth sign in");
            Logger.LogError(ex, "Facebook sign in error");
        }
        finally
        {
            // Don't set loading to false here because we're redirecting
            // isLoading = false;
            StateHasChanged();
        }
    }

    // ==================== VALIDATION ====================

    private bool ValidateForm()
    {
        bool isValid = true;
        
        // Validate email
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
        
        // Validate password
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
