// Pages/Auth/SignUp.razor.cs - WITH GOOGLE OAUTH
using Microsoft.AspNetCore.Components;
using SubashaVentures.Services.Auth;
using SubashaVentures.Models.Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Microsoft.AspNetCore.Components.Authorization;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Auth;

public partial class SignUp : ComponentBase
{
    [Inject] private SupabaseAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<SignUp> Logger { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private string firstName = "";
    private string lastName = "";
    private string email = "";
    private string password = "";
    private string confirmPassword = "";
    private bool acceptTerms = false;
    
    private bool showPassword = false;
    private bool showConfirmPassword = false;
    
    private string firstNameError = "";
    private string lastNameError = "";
    private string emailError = "";
    private string passwordError = "";
    private string confirmPasswordError = "";
    private string termsError = "";
    private string generalError = "";
    private string successMessage = "";
    
    private bool isLoading = false;

    protected override async Task OnInitializedAsync()
    {
        // Check if already authenticated
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        if (authState.User?.Identity?.IsAuthenticated ?? false)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "User already authenticated, redirecting to home",
                LogLevel.Info
            );

            NavigationManager.NavigateTo("/", forceLoad: false);
        }
    }

    // ==================== EMAIL/PASSWORD SIGN UP ====================
    
    private async Task HandleSignUp()
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
                $"Starting sign up for: {email}",
                LogLevel.Info
            );

            var userData = new UserModel
            {
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                Email = email.Trim().ToLowerInvariant(),
                EmailNotifications = true,
                SmsNotifications = false,
                PreferredLanguage = "en",
                Currency = "NGN",
                AccountStatus = "Active",
                MembershipTier = "Bronze",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };

            var result = await AuthService.SignUpAsync(email, password, userData);
            
            if (result.Success)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✓ User signed up successfully: {email}",
                    LogLevel.Info
                );

                Logger.LogInformation("User signed up successfully: {Email}", email);
                
                successMessage = result.Message;
                
                // Notify auth state if needed
                if (AuthStateProvider is SupabaseAuthStateProvider provider)
                {
                    provider.NotifyAuthenticationStateChanged();
                }

                // Wait and redirect to sign-in
                await Task.Delay(3000);
                NavigationManager.NavigateTo("/signin?registered=true", forceLoad: false);
            }
            else
            {
                // Check for specific errors
                if (result.ErrorCode == "user_already_exists")
                {
                    emailError = result.Message;
                }
                else
                {
                    generalError = result.Message;
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

    // ==================== GOOGLE OAUTH SIGN UP ====================
    
    private async Task HandleGoogleSignUp()
    {
        try
        {
            ClearErrors();
            isLoading = true;
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                "Initiating Google sign-up",
                LogLevel.Info
            );

            // Initiate Google OAuth (this will redirect to Google)
            // After successful authentication, user will be created automatically
            var success = await AuthService.SignInWithGoogleAsync("/");

            if (!success)
            {
                generalError = "Failed to initiate Google sign-up. Please try again.";
                isLoading = false;
                StateHasChanged();
            }
            // Note: If successful, user will be redirected to Google
            // They'll come back to /auth/callback after authentication
        }
        catch (Exception ex)
        {
            generalError = "An error occurred with Google sign-up. Please try again.";
            await MID_HelperFunctions.LogExceptionAsync(ex, "Google sign-up");
            Logger.LogError(ex, "Error during Google sign-up");
            
            isLoading = false;
            StateHasChanged();
        }
    }

    // ==================== VALIDATION ====================
    
    private bool ValidateForm()
    {
        bool isValid = true;
        
        // First name validation
        if (string.IsNullOrWhiteSpace(firstName))
        {
            firstNameError = "First name is required";
            isValid = false;
        }
        else if (firstName.Trim().Length < 2)
        {
            firstNameError = "First name must be at least 2 characters";
            isValid = false;
        }
        else if (!IsValidName(firstName))
        {
            firstNameError = "First name contains invalid characters";
            isValid = false;
        }
        
        // Last name validation
        if (string.IsNullOrWhiteSpace(lastName))
        {
            lastNameError = "Last name is required";
            isValid = false;
        }
        else if (lastName.Trim().Length < 2)
        {
            lastNameError = "Last name must be at least 2 characters";
            isValid = false;
        }
        else if (!IsValidName(lastName))
        {
            lastNameError = "Last name contains invalid characters";
            isValid = false;
        }
        
        // Email validation
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
        
        // Password validation
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
        
        // Confirm password validation
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
        
        // Terms acceptance validation
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
        // Allow letters, spaces, hyphens, and apostrophes
        return name.Trim().All(c => char.IsLetter(c) || c == ' ' || c == '-' || c == '\'');
    }

    private static bool IsStrongPassword(string password)
    {
        bool hasUpper = password.Any(char.IsUpper);
        bool hasLower = password.Any(char.IsLower);
        bool hasDigit = password.Any(char.IsDigit);
        
        return hasUpper && hasLower && hasDigit;
    }
}
