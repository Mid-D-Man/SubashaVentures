// Pages/Auth/SignUp.razor.cs - UPDATED with OAuth
using Microsoft.AspNetCore.Components;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Microsoft.AspNetCore.Components.Authorization;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Auth;

public partial class SignUp : ComponentBase
{
    [Inject] private ISupabaseAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<SignUp> Logger { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

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
    private string generalError = "";
    private string successMessage = "";
    
    // Loading state
    private bool isLoading = false;

    // ==================== EMAIL/PASSWORD SIGN UP ====================

    private async Task HandleSignUp()
    {
        // Clear previous messages
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
                $"Starting sign up for: {email}",
                LogLevel.Info
            );

            // Create user data
            var userData = new UserModel
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                EmailNotifications = true,
                SmsNotifications = false,
                PreferredLanguage = "en",
                Currency = "NGN",
                AccountStatus = "Active",
                MembershipTier = "Bronze",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };

            // Attempt sign up with Supabase
            var result = await AuthService.SignUpAsync(email, password, userData);
            
            if (result.Success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ User signed up successfully: {email}",
                    LogLevel.Info
                );

                Logger.LogInformation("User signed up successfully: {Email}", email);
                
                // Show success message
                successMessage = "Account created! Please check your email to verify your account.";
                
                // Notify auth state changed
                if (AuthStateProvider is SupabaseAuthStateProvider provider)
                {
                    provider.NotifyAuthenticationStateChanged();
                }

                // Wait 3 seconds then redirect to sign in
                await Task.Delay(3000);
                NavigationManager.NavigateTo("/signin?registered=true");
            }
            else
            {
                // Handle sign up failure
                if (result.ErrorCode == "USER_EXISTS")
                {
                    emailError = "An account with this email already exists. Please sign in instead.";
                }
                else
                {
                    generalError = result.Message ?? "Failed to create account. Please try again.";
                }
                
                Logger.LogWarning("Sign up failed for {Email}: {Message}", email, result.Message);
            }
        }
        catch (Exception ex)
        {
            generalError = "An error occurred during registration. Please try again later.";
            await MID_HelperFunctions.LogExceptionAsync(ex, "Sign up");
            Logger.LogError(ex, "Error during sign up for {Email}", email);
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    // ==================== OAUTH SIGN UP ====================

    private async Task HandleGoogleSignUp()
    {
        try
        {
            isLoading = true;
            ClearErrors();
            successMessage = "";
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                "Initiating Google OAuth sign up",
                LogLevel.Info
            );
            
            var success = await AuthService.SignInWithGoogleAsync();
            
            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✓ Google OAuth redirect initiated",
                    LogLevel.Info
                );

                // The browser will redirect to Google
                // When user returns, they'll be authenticated
                successMessage = "Redirecting to Google...";
            }
            else
            {
                generalError = "Failed to connect to Google. Please try again.";
                Logger.LogError("Google OAuth initiation failed");
            }
        }
        catch (Exception ex)
        {
            generalError = "Failed to sign up with Google. Please try again.";
            await MID_HelperFunctions.LogExceptionAsync(ex, "Google OAuth sign up");
            Logger.LogError(ex, "Google sign up error");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task HandleFacebookSignUp()
    {
        try
        {
            isLoading = true;
            ClearErrors();
            successMessage = "";
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                "Initiating Facebook OAuth sign up",
                LogLevel.Info
            );
            
            var success = await AuthService.SignInWithFacebookAsync();
            
            if (success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "✓ Facebook OAuth redirect initiated",
                    LogLevel.Info
                );

                // The browser will redirect to Facebook
                successMessage = "Redirecting to Facebook...";
            }
            else
            {
                generalError = "Failed to connect to Facebook. Please try again.";
                Logger.LogError("Facebook OAuth initiation failed");
            }
        }
        catch (Exception ex)
        {
            generalError = "Failed to sign up with Facebook. Please try again.";
            await MID_HelperFunctions.LogExceptionAsync(ex, "Facebook OAuth sign up");
            Logger.LogError(ex, "Facebook sign up error");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    // ==================== VALIDATION ====================

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
        else if (!IsValidName(firstName))
        {
            firstNameError = "First name contains invalid characters";
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
        else if (!IsValidName(lastName))
        {
            lastNameError = "Last name contains invalid characters";
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

    private static bool IsValidName(string name)
    {
        // Only allow letters, spaces, hyphens, and apostrophes
        return name.All(c => char.IsLetter(c) || c == ' ' || c == '-' || c == '\'');
    }

    private static bool IsStrongPassword(string password)
    {
        // Check for at least one uppercase, one lowercase, and one digit
        bool hasUpper = password.Any(char.IsUpper);
        bool hasLower = password.Any(char.IsLower);
        bool hasDigit = password.Any(char.IsDigit);
        
        return hasUpper && hasLower && hasDigit;
    }
}
