// Pages/User/Settings.razor.cs - COMPLETE IMPLEMENTATION
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Domain.User;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.Users;
using SubashaVentures.Utilities.HelperScripts;
using System.Text;
using System.Text.Json;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.User;

public partial class Settings
{
    [Inject] private IUserService UserService { get; set; } = default!;
    [Inject] private IPermissionService PermissionService { get; set; } = default!;
    [Inject] private ISupabaseAuthService AuthService { get; set; } = default!;
    [Inject] private ISupabaseStorageService StorageService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    // ==================== STATE ====================
    private bool IsLoading = true;
    private bool IsProcessing = false;
    private string ProcessingMessage = "Processing...";
    private UserProfileViewModel? UserProfile;
    private string? CurrentUserId;

    // ==================== PROFILE EDIT ====================
    private DynamicModal? ProfileModal;
    private bool IsProfileModalOpen = false;
    private bool IsSavingProfile = false;
    private bool IsUploadingAvatar = false;
    
    private string TempFirstName = "";
    private string TempLastName = "";
    private string TempNickname = "";
    private string TempPhoneNumber = "";
    private string TempAvatarUrl = "";
    private DateTime? TempDateOfBirth;
    private string TempGender = "";

    // ==================== NOTIFICATIONS ====================
    private bool EmailNotificationsEnabled = true;
    private bool SmsNotificationsEnabled = false;

    // ==================== PREFERENCES ====================
    private DynamicModal? CurrencyModal;
    private DynamicModal? LanguageModal;
    private bool IsCurrencyModalOpen = false;
    private bool IsLanguageModalOpen = false;
    
    private string SelectedCurrency = "NGN";
    private string SelectedLanguage = "English";

    private readonly List<CurrencyOption> AvailableCurrencies = new()
    {
        new CurrencyOption { Code = "NGN", Name = "Nigerian Naira (₦)" },
        new CurrencyOption { Code = "USD", Name = "US Dollar ($)" },
        new CurrencyOption { Code = "GBP", Name = "British Pound (£)" },
        new CurrencyOption { Code = "EUR", Name = "Euro (€)" }
    };

    private readonly List<string> AvailableLanguages = new()
    {
        "English",
        "Hausa",
        "Yoruba",
        "Igbo"
    };

    // ==================== SECURITY ====================
    private DynamicModal? SecurityModal;
    private bool IsSecurityModalOpen = false;
    private bool IsChangingPassword = false;
    
    private string CurrentPassword = "";
    private string NewPassword = "";
    private string ConfirmPassword = "";
    private string PasswordChangeError = "";

    // ==================== MFA ====================
    private DynamicModal? MfaModal;
    private bool IsMfaModalOpen = false;
    private bool IsMfaEnabled = false;
    private bool IsProcessingMfa = false;
    private int MfaEnrollmentStep = 0; // 0 = Info, 1 = Enrollment
    
    private string MfaQrCodeUrl = "";
    private string MfaSecret = "";
    private string MfaVerificationCode = "";
    private string MfaEnrollmentError = "";
    private string? MfaFactorId;

    // ==================== LOGOUT ====================
    private ConfirmationPopup? LogoutPopup;
    private bool ShowLogoutPopup = false;

    // ==================== LIFECYCLE ====================
    
    protected override async Task OnInitializedAsync()
    {
        await LoadUserSettings();
    }

    private async Task LoadUserSettings()
    {
        IsLoading = true;
        StateHasChanged();

        try
        {
            // Get current user ID
            CurrentUserId = await PermissionService.GetCurrentUserIdAsync();
            
            if (string.IsNullOrEmpty(CurrentUserId))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "User not authenticated - redirecting to sign in",
                    LogLevel.Warning
                );
                Navigation.NavigateTo("/signin");
                return;
            }

            // Load user profile
            UserProfile = await UserService.GetUserByIdAsync(CurrentUserId);
            
            if (UserProfile == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "User profile not found",
                    LogLevel.Error
                );
                Navigation.NavigateTo("/");
                return;
            }

            // Set initial values
            EmailNotificationsEnabled = UserProfile.EmailNotifications;
            SmsNotificationsEnabled = UserProfile.SmsNotifications;
            SelectedCurrency = UserProfile.Currency;
            SelectedLanguage = UserProfile.PreferredLanguage switch
            {
                "en" => "English",
                "ha" => "Hausa",
                "yo" => "Yoruba",
                "ig" => "Igbo",
                _ => "English"
            };

            // Check MFA status
            await CheckMfaStatus();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Settings loaded for user: {UserProfile.Email}",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading settings");
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    // ==================== PROFILE METHODS ====================

    private void OpenProfileModal()
    {
        if (UserProfile == null) return;

        TempFirstName = UserProfile.FirstName;
        TempLastName = UserProfile.LastName;
        TempNickname = UserProfile.Nickname ?? "";
        TempPhoneNumber = UserProfile.PhoneNumber ?? "";
        TempAvatarUrl = UserProfile.AvatarUrl ?? "";
        TempDateOfBirth = UserProfile.DateOfBirth;
        TempGender = UserProfile.Gender ?? "";

        IsProfileModalOpen = true;
        StateHasChanged();
    }

    private void CloseProfileModal()
    {
        IsProfileModalOpen = false;
        StateHasChanged();
    }

    private async Task HandleAvatarUpload(InputFileChangeEventArgs e)
    {
        try
        {
            var file = e.File;
            
            if (file == null)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No file selected",
                    LogLevel.Warning
                );
                return;
            }

            // Validate file type
            if (!file.ContentType.StartsWith("image/"))
            {
                await JSRuntime.InvokeVoidAsync("alert", "Please select an image file");
                return;
            }

            // Validate file size (5MB max)
            const long maxFileSize = 5 * 1024 * 1024;
            if (file.Size > maxFileSize)
            {
                await JSRuntime.InvokeVoidAsync("alert", "Image must be less than 5MB");
                return;
            }

            IsUploadingAvatar = true;
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Uploading avatar: {file.Name} ({file.Size} bytes)",
                LogLevel.Info
            );

            // Upload to Supabase Storage
            var result = await StorageService.UploadImageAsync(
                file,
                bucketName: "avatars",
                folder: $"users/{CurrentUserId}",
                enableCompression: true
            );

            if (result.Success && !string.IsNullOrEmpty(result.PublicUrl))
            {
                TempAvatarUrl = result.PublicUrl;
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Avatar uploaded successfully: {result.PublicUrl}",
                    LogLevel.Info
                );
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("alert", $"Upload failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Uploading avatar");
            await JSRuntime.InvokeVoidAsync("alert", "Failed to upload image");
        }
        finally
        {
            IsUploadingAvatar = false;
            StateHasChanged();
        }
    }

    private async Task SaveProfile()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(TempFirstName) || string.IsNullOrWhiteSpace(TempLastName))
            {
                await JSRuntime.InvokeVoidAsync("alert", "First name and last name are required");
                return;
            }

            IsSavingProfile = true;
            StateHasChanged();

            var updateRequest = new UpdateUserRequest
            {
                FirstName = TempFirstName.Trim(),
                LastName = TempLastName.Trim(),
                Nickname = string.IsNullOrWhiteSpace(TempNickname) ? null : TempNickname.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(TempPhoneNumber) ? null : TempPhoneNumber.Trim(),
                AvatarUrl = string.IsNullOrWhiteSpace(TempAvatarUrl) ? null : TempAvatarUrl,
                DateOfBirth = TempDateOfBirth,
                Gender = string.IsNullOrWhiteSpace(TempGender) ? null : TempGender
            };

            var success = await UserService.UpdateUserProfileAsync(CurrentUserId!, updateRequest);

            if (success)
            {
                await JSRuntime.InvokeVoidAsync("alert", "Profile updated successfully!");
                
                // Reload profile
                await LoadUserSettings();
                
                CloseProfileModal();
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("alert", "Failed to update profile");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Saving profile");
            await JSRuntime.InvokeVoidAsync("alert", "An error occurred while saving");
        }
        finally
        {
            IsSavingProfile = false;
            StateHasChanged();
        }
    }

    // ==================== NOTIFICATION METHODS ====================

    private async Task ToggleEmailNotifications(ChangeEventArgs e)
    {
        try
        {
            var enabled = (bool)(e.Value ?? false);
            
            var updateRequest = new UpdateUserRequest
            {
                EmailNotifications = enabled
            };

            var success = await UserService.UpdateUserProfileAsync(CurrentUserId!, updateRequest);

            if (success)
            {
                EmailNotificationsEnabled = enabled;
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Email notifications {(enabled ? "enabled" : "disabled")}",
                    LogLevel.Info
                );
            }
            else
            {
                // Revert toggle
                EmailNotificationsEnabled = !enabled;
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Toggling email notifications");
        }
    }

    private async Task ToggleSmsNotifications(ChangeEventArgs e)
    {
        try
        {
            var enabled = (bool)(e.Value ?? false);
            
            var updateRequest = new UpdateUserRequest
            {
                SmsNotifications = enabled
            };

            var success = await UserService.UpdateUserProfileAsync(CurrentUserId!, updateRequest);

            if (success)
            {
                SmsNotificationsEnabled = enabled;
                await MID_HelperFunctions.DebugMessageAsync(
                    $"SMS notifications {(enabled ? "enabled" : "disabled")}",
                    LogLevel.Info
                );
            }
            else
            {
                // Revert toggle
                SmsNotificationsEnabled = !enabled;
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Toggling SMS notifications");
        }
    }

    // ==================== PREFERENCE METHODS ====================

    private void OpenCurrencyModal()
    {
        IsCurrencyModalOpen = true;
        StateHasChanged();
    }

    private void CloseCurrencyModal()
    {
        IsCurrencyModalOpen = false;
        StateHasChanged();
    }

    private async Task SelectCurrency(string currencyCode)
    {
        try
        {
            var updateRequest = new UpdateUserRequest
            {
                Currency = currencyCode
            };

            var success = await UserService.UpdateUserProfileAsync(CurrentUserId!, updateRequest);

            if (success)
            {
                SelectedCurrency = currencyCode;
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Currency changed to: {currencyCode}",
                    LogLevel.Info
                );
            }

            CloseCurrencyModal();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Selecting currency");
        }
    }

    private void OpenLanguageModal()
    {
        IsLanguageModalOpen = true;
        StateHasChanged();
    }

    private void CloseLanguageModal()
    {
        IsLanguageModalOpen = false;
        StateHasChanged();
    }

    private async Task SelectLanguage(string language)
    {
        try
        {
            var languageCode = language switch
            {
                "English" => "en",
                "Hausa" => "ha",
                "Yoruba" => "yo",
                "Igbo" => "ig",
                _ => "en"
            };

            var updateRequest = new UpdateUserRequest
            {
                PreferredLanguage = languageCode
            };

            var success = await UserService.UpdateUserProfileAsync(CurrentUserId!, updateRequest);

            if (success)
            {
                SelectedLanguage = language;
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Language changed to: {language}",
                    LogLevel.Info
                );
            }

            CloseLanguageModal();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Selecting language");
        }
    }

    // ==================== SECURITY METHODS ====================

    private void OpenSecurityModal()
    {
        CurrentPassword = "";
        NewPassword = "";
        ConfirmPassword = "";
        PasswordChangeError = "";
        IsSecurityModalOpen = true;
        StateHasChanged();
    }

    private void CloseSecurityModal()
    {
        IsSecurityModalOpen = false;
        StateHasChanged();
    }

    private async Task ChangePassword()
    {
        try
        {
            PasswordChangeError = "";

            // Validation
            if (string.IsNullOrWhiteSpace(CurrentPassword) || 
                string.IsNullOrWhiteSpace(NewPassword) || 
                string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                PasswordChangeError = "All fields are required";
                return;
            }

            if (NewPassword.Length < 8)
            {
                PasswordChangeError = "New password must be at least 8 characters";
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                PasswordChangeError = "Passwords do not match";
                return;
            }

            IsChangingPassword = true;
            StateHasChanged();

            // Change password via Supabase Auth
            var result = await AuthService.ChangePasswordAsync(NewPassword);

            if (result.Success)
            {
                await JSRuntime.InvokeVoidAsync("alert", "Password changed successfully!");
                CloseSecurityModal();
            }
            else
            {
                PasswordChangeError = result.Message ?? "Failed to change password";
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Changing password");
            PasswordChangeError = "An error occurred. Please try again.";
        }
        finally
        {
            IsChangingPassword = false;
            StateHasChanged();
        }
    }
// Pages/User/Settings.razor.cs - CONTINUATION (MFA & Helper Methods)
// Add these methods to the existing partial class

    // ==================== MFA METHODS ====================

    private async Task CheckMfaStatus()
    {
        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            // Check AAL (Authenticator Assurance Level) from JWT
            var aalClaim = user?.FindFirst("aal")?.Value;
            IsMfaEnabled = aalClaim == "aal2";

            await MID_HelperFunctions.DebugMessageAsync(
                $"MFA Status: {(IsMfaEnabled ? "Enabled (aal2)" : "Disabled (aal1)")}",
                LogLevel.Info
            );

            // Also check enrolled factors
            var factors = await AuthService.GetMfaFactorsAsync();
            if (factors != null && factors.Any())
            {
                IsMfaEnabled = true;
                MfaFactorId = factors.FirstOrDefault()?.Id;
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Found {factors.Count} MFA factor(s) enrolled",
                    LogLevel.Info
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Checking MFA status");
        }
    }

    private void OpenMfaModal()
    {
        MfaEnrollmentStep = 0;
        MfaVerificationCode = "";
        MfaEnrollmentError = "";
        IsMfaModalOpen = true;
        StateHasChanged();
    }

    private void CloseMfaModal()
    {
        IsMfaModalOpen = false;
        MfaEnrollmentStep = 0;
        MfaQrCodeUrl = "";
        MfaSecret = "";
        MfaVerificationCode = "";
        MfaEnrollmentError = "";
        StateHasChanged();
    }

    private async Task StartMfaEnrollment()
    {
        try
        {
            IsProcessingMfa = true;
            MfaEnrollmentError = "";
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                "Starting MFA enrollment...",
                LogLevel.Info
            );

            // Enroll TOTP factor via Supabase Auth
            var enrollResult = await AuthService.EnrollMfaAsync("totp");

            if (enrollResult.Success && enrollResult.QrCodeUrl != null && enrollResult.Secret != null)
            {
                MfaQrCodeUrl = enrollResult.QrCodeUrl;
                MfaSecret = enrollResult.Secret;
                MfaFactorId = enrollResult.FactorId;
                MfaEnrollmentStep = 1;

                await MID_HelperFunctions.DebugMessageAsync(
                    $"MFA enrollment initiated. Factor ID: {MfaFactorId}",
                    LogLevel.Info
                );
            }
            else
            {
                MfaEnrollmentError = enrollResult.ErrorMessage ?? "Failed to start MFA enrollment";
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"MFA enrollment failed: {MfaEnrollmentError}",
                    LogLevel.Error
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Starting MFA enrollment");
            MfaEnrollmentError = "An error occurred. Please try again.";
        }
        finally
        {
            IsProcessingMfa = false;
            StateHasChanged();
        }
    }

    private async Task VerifyAndEnableMfa()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(MfaVerificationCode) || MfaVerificationCode.Length != 6)
            {
                MfaEnrollmentError = "Please enter a valid 6-digit code";
                return;
            }

            if (string.IsNullOrEmpty(MfaFactorId))
            {
                MfaEnrollmentError = "Invalid enrollment state. Please restart.";
                return;
            }

            IsProcessingMfa = true;
            MfaEnrollmentError = "";
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Verifying MFA code for factor: {MfaFactorId}",
                LogLevel.Info
            );

            // Verify and enable MFA
            var verifyResult = await AuthService.VerifyMfaAsync(MfaFactorId, MfaVerificationCode);

            if (verifyResult.Success)
            {
                IsMfaEnabled = true;
                
                await JSRuntime.InvokeVoidAsync("alert", 
                    "Two-Factor Authentication enabled successfully!\n\n" +
                    "⚠️ IMPORTANT: Save your recovery codes in a safe place. " +
                    "You'll need them if you lose access to your authenticator app.");

                await MID_HelperFunctions.DebugMessageAsync(
                    "MFA enabled successfully",
                    LogLevel.Info
                );

                CloseMfaModal();
            }
            else
            {
                MfaEnrollmentError = verifyResult.ErrorMessage ?? "Invalid verification code";
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"MFA verification failed: {MfaEnrollmentError}",
                    LogLevel.Warning
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Verifying MFA");
            MfaEnrollmentError = "Verification failed. Please check your code and try again.";
        }
        finally
        {
            IsProcessingMfa = false;
            StateHasChanged();
        }
    }

    private void CancelMfaEnrollment()
    {
        MfaEnrollmentStep = 0;
        MfaQrCodeUrl = "";
        MfaSecret = "";
        MfaVerificationCode = "";
        MfaEnrollmentError = "";
        StateHasChanged();
    }

    private async Task DisableMfa()
    {
        try
        {
            var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", 
                "Are you sure you want to disable Two-Factor Authentication?\n\n" +
                "This will make your account less secure.");

            if (!confirmed) return;

            IsProcessingMfa = true;
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                "Disabling MFA...",
                LogLevel.Warning
            );

            // Get all enrolled factors
            var factors = await AuthService.GetMfaFactorsAsync();
            
            if (factors != null && factors.Any())
            {
                // Unenroll each factor
                foreach (var factor in factors)
                {
                    var result = await AuthService.UnenrollMfaAsync(factor.Id);
                    
                    if (!result.Success)
                    {
                        await JSRuntime.InvokeVoidAsync("alert", 
                            $"Failed to disable MFA: {result.ErrorMessage}");
                        return;
                    }
                }

                IsMfaEnabled = false;
                
                await JSRuntime.InvokeVoidAsync("alert", 
                    "Two-Factor Authentication has been disabled.");

                await MID_HelperFunctions.DebugMessageAsync(
                    "MFA disabled successfully",
                    LogLevel.Warning
                );

                CloseMfaModal();
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("alert", "No MFA factors found to disable.");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Disabling MFA");
            await JSRuntime.InvokeVoidAsync("alert", "Failed to disable MFA. Please try again.");
        }
        finally
        {
            IsProcessingMfa = false;
            StateHasChanged();
        }
    }

    // ==================== EXPORT DATA ====================

    private async Task ExportMyData()
    {
        try
        {
            IsProcessing = true;
            ProcessingMessage = "Preparing your data...";
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Exporting data for user: {CurrentUserId}",
                LogLevel.Info
            );

            if (UserProfile == null)
            {
                await JSRuntime.InvokeVoidAsync("alert", "Unable to export data. Please try again.");
                return;
            }

            // Create export data object
            var exportData = new
            {
                ExportDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                UserProfile = new
                {
                    UserProfile.Id,
                    UserProfile.Email,
                    UserProfile.FirstName,
                    UserProfile.LastName,
                    UserProfile.Nickname,
                    UserProfile.PhoneNumber,
                    UserProfile.DateOfBirth,
                    UserProfile.Gender,
                    UserProfile.AccountStatus,
                    UserProfile.MemberSince,
                    UserProfile.LastLoginAt
                },
                Statistics = new
                {
                    UserProfile.TotalOrders,
                    UserProfile.TotalSpent,
                    UserProfile.LoyaltyPoints,
                    UserProfile.MembershipTier
                },
                Preferences = new
                {
                    UserProfile.EmailNotifications,
                    UserProfile.SmsNotifications,
                    UserProfile.PreferredLanguage,
                    UserProfile.Currency
                },
                Security = new
                {
                    UserProfile.IsEmailVerified,
                    UserProfile.IsPhoneVerified,
                    MfaEnabled = IsMfaEnabled
                }
            };

            // Convert to JSON
            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });

            // Download as file
            var fileName = $"SubashaVentures_UserData_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var bytes = Encoding.UTF8.GetBytes(json);
            var base64 = Convert.ToBase64String(bytes);

            await JSRuntime.InvokeVoidAsync("downloadFile", fileName, base64, "application/json");

            await JSRuntime.InvokeVoidAsync("alert", "Your data has been exported successfully!");

            await MID_HelperFunctions.DebugMessageAsync(
                "Data exported successfully",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Exporting user data");
            await JSRuntime.InvokeVoidAsync("alert", "Failed to export data. Please try again.");
        }
        finally
        {
            IsProcessing = false;
            StateHasChanged();
        }
    }

    // ==================== LOGOUT ====================

    private void ShowLogoutConfirmation()
    {
        ShowLogoutPopup = true;
        StateHasChanged();
    }

    private void CancelLogout()
    {
        ShowLogoutPopup = false;
        StateHasChanged();
    }

    private async Task ConfirmLogout()
    {
        try
        {
            ShowLogoutPopup = false;
            IsProcessing = true;
            ProcessingMessage = "Logging out...";
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                "User logging out...",
                LogLevel.Info
            );

            // Sign out via Supabase Auth
            await AuthService.SignOutAsync();

            // Clear any local cached data if needed
            // await LocalStorageService.ClearAllAsync();

            await MID_HelperFunctions.DebugMessageAsync(
                "User logged out successfully",
                LogLevel.Info
            );

            // Redirect to home page
            Navigation.NavigateTo("/", forceLoad: true);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Logging out");
            await JSRuntime.InvokeVoidAsync("alert", "Failed to logout. Please try again.");
        }
        finally
        {
            IsProcessing = false;
            StateHasChanged();
        }
    }

    // ==================== HELPER METHODS ====================

    private string GetMembershipBadgeClass()
    {
        return UserProfile?.MembershipTier switch
        {
            MembershipTier.Platinum => "platinum",
            MembershipTier.Gold => "gold",
            MembershipTier.Silver => "silver",
            MembershipTier.Bronze => "bronze",
            _ => "secondary"
        };
    }
}
    // Continue to Response 3 for MFA methods...
}

public class CurrencyOption
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}
