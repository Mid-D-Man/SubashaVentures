

}
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
