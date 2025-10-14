using Microsoft.AspNetCore.Components;
using System.ComponentModel.DataAnnotations;

namespace SubashaVentures.Pages.Auth;

public partial class SignUp : ComponentBase
{
    // Form fields
    private string firstName = "";
    private string lastName = "";
    private string email = "";
    private string password = "";
    private string confirmPassword = "";
    private bool acceptTerms = false;
    
    // Password visibility toggles
    private bool showPassword = false;
    private bool showConfirmPassword = false;
    
    // Error messages
    private string firstNameError = "";
    private string lastNameError = "";
    private string emailError = "";
    private string passwordError = "";
    private string confirmPasswordError = "";
    private string termsError = "";
    
    // Loading state
    private bool isLoading = false;

    private async Task HandleSignUp()
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
            
            // In real app: await AuthService.SignUp(firstName, lastName, email, password);
            
            // Success - navigate to sign in or dashboard
            Console.WriteLine($"Sign up successful for: {email}");
            // NavigationManager.NavigateTo("/signin");
        }
        catch (Exception ex)
        {
            // Handle sign up error
            emailError = "An error occurred during sign up. Please try again.";
            Console.WriteLine($"Sign up error: {ex.Message}");
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
        
        // Validate first name
        if (string.IsNullOrWhiteSpace(firstName))
        {
            firstNameError = "First name is required";
            isValid = false;
        }
        else if (firstName.Length < 2)
        {
            firstNameError = "First name must be at least 2 characters";
            isValid = false;
        }
        
        // Validate last name
        if (string.IsNullOrWhiteSpace(lastName))
        {
            lastNameError = "Last name is required";
            isValid = false;
        }
        else if (lastName.Length < 2)
        {
            lastNameError = "Last name must be at least 2 characters";
            isValid = false;
        }
        
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
        else if (password.Length < 8)
        {
            passwordError = "Password must be at least 8 characters";
            isValid = false;
        }
        else if (!IsStrongPassword(password))
        {
            passwordError = "Password must contain uppercase, lowercase, and number";
            isValid = false;
        }
        
        // Validate confirm password
        if (string.IsNullOrWhiteSpace(confirmPassword))
        {
            confirmPasswordError = "Please confirm your password";
            isValid = false;
        }
        else if (password != confirmPassword)
        {
            confirmPasswordError = "Passwords do not match";
            isValid = false;
        }
        
        // Validate terms acceptance
        if (!acceptTerms)
        {
            termsError = "You must accept the terms and conditions";
            isValid = false;
        }
        
        return isValid;
    }

    private void ClearErrors()
    {
        firstNameError = "";
        lastNameError = "";
        emailError = "";
        passwordError = "";
        confirmPasswordError = "";
        termsError = "";
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

    private static bool IsStrongPassword(string password)
    {
        // Check for at least one uppercase, one lowercase, and one digit
        bool hasUpper = password.Any(char.IsUpper);
        bool hasLower = password.Any(char.IsLower);
        bool hasDigit = password.Any(char.IsDigit);
        
        return hasUpper && hasLower && hasDigit;
    }

    private async Task HandleGoogleSignUp()
    {
        // Implement Google OAuth sign up
        Console.WriteLine("Google sign up clicked");
        await Task.CompletedTask;
        // In real app: await AuthService.SignUpWithGoogle();
    }

    private async Task HandleFacebookSignUp()
    {
        // Implement Facebook OAuth sign up
        Console.WriteLine("Facebook sign up clicked");
        await Task.CompletedTask;
        // In real app: await AuthService.SignUpWithFacebook();
    }
}
