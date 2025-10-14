using Microsoft.AspNetCore.Components;

namespace SubashaVentures.Pages.Auth;

public partial class SignIn : ComponentBase
{
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
            // Simulate API call
            await Task.Delay(1500);
            
            // In real app: 
            // var result = await AuthService.SignIn(email, password, rememberMe);
            // if (result.Success) { ... }
            
            // For demo - simulate success
            Console.WriteLine($"Sign in successful for: {email}");
            
            // Navigate to home or dashboard
            // NavigationManager.NavigateTo("/");
        }
        catch (UnauthorizedAccessException)
        {
            generalError = "Invalid email or password. Please try again.";
        }
        catch (Exception ex)
        {
            generalError = "An error occurred during sign in. Please try again later.";
            Console.WriteLine($"Sign in error: {ex.Message}");
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
        // Implement Google OAuth sign in
        Console.WriteLine("Google sign in clicked");
        await Task.CompletedTask;
        
        // In real app:
        // await AuthService.SignInWithGoogle();
        // NavigationManager.NavigateTo("/");
    }

    private async Task HandleFacebookSignIn()
    {
        // Implement Facebook OAuth sign in
        Console.WriteLine("Facebook sign in clicked");
        await Task.CompletedTask;
        
        // In real app:
        // await AuthService.SignInWithFacebook();
        // NavigationManager.NavigateTo("/");
    }
}
