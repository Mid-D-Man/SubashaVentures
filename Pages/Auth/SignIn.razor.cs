// Pages/Auth/SignIn.razor.cs
using Microsoft.AspNetCore.Components;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Utilities.HelperScripts;

namespace SubashaVentures.Pages.Auth;

public partial class SignIn : ComponentBase
{
    [Inject] private ISupabaseAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<SignIn> Logger { get; set; } = default!;

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
    
    // Loading state
    private bool isLoading = false;

    private async Task HandleSignIn()
    {
        // Clear previous errors
        ClearErrors();
        
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
            // Attempt sign in with Supabase
            var result = await AuthService.SignInAsync(email, password);
            
            if (result.Success)
            {
                Logger.LogInformation("User signed in successfully: {Email}", email);
                
                // Navigate to home page or previous page
                NavigationManager.NavigateTo("/", forceLoad: true);
            }
            else
            {
                // Handle sign in failure
                generalError = result.Message ?? "Invalid email or password. Please try again.";
                Logger.LogWarning("Sign in failed for {Email}: {Message}", email, result.Message);
            }
        }
        catch (UnauthorizedAccessException)
        {
            generalError = "Invalid email or password. Please try again.";
            Logger.LogWarning("Unauthorized access attempt for {Email}", email);
        }
        catch (Exception ex)
        {
            generalError = "An error occurred during sign in. Please try again later.";
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

    private async Task HandleGoogleSignIn()
    {
        try
        {
            isLoading = true;
            StateHasChanged();
            
            // Google OAuth sign in
            Logger.LogInformation("Attempting Google sign in");
            
            // Note: OAuth redirect flows work differently in Blazor WASM
            // You'll need to implement this based on Supabase's OAuth flow
            generalError = "Google sign in is not yet implemented. Please use email/password.";
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            generalError = "Failed to sign in with Google. Please try again.";
            Logger.LogError(ex, "Google sign in error");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task HandleFacebookSignIn()
    {
        try
        {
            isLoading = true;
            StateHasChanged();
            
            // Facebook OAuth sign in
            Logger.LogInformation("Attempting Facebook sign in");
            
            generalError = "Facebook sign in is not yet implemented. Please use email/password.";
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            generalError = "Failed to sign in with Facebook. Please try again.";
            Logger.LogError(ex, "Facebook sign in error");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }
}
