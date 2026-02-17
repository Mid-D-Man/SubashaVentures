// Pages/User/Settings.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Domain.User;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.Users;
using SubashaVentures.Services.VisualElements;
using SubashaVentures.Utilities.HelperScripts;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
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
    [Inject] private IVisualElementsService VisualElements { get; set; } = default!;

    // STATE
    private bool IsLoading = true;
    private UserProfileViewModel? UserProfile;
    private string? CurrentUserId;
    private bool CanChangePassword = true;

    // SVG MARKUP
    private string profileIconSvg = string.Empty;
    private string notificationIconSvg = string.Empty;
    private string emailIconSvg = string.Empty;
    private string smsIconSvg = string.Empty;
    private string settingsIconSvg = string.Empty;
    private string currencyIconSvg = string.Empty;
    private string languageIconSvg = string.Empty;
    private string securityIconSvg = string.Empty;
    private string keyIconSvg = string.Empty;
    private string mfaIconSvg = string.Empty;
    private string infoIconSvg = string.Empty;
    private string downloadIconSvg = string.Empty;
    private string deleteIconSvg = string.Empty;
    private string logoutIconSvg = string.Empty;
    private string arrowRightSvg = string.Empty;
    private string checkIconSvg = string.Empty;
    private string warningIconSvg = string.Empty;
    private string cameraIconSvg = string.Empty;
    private string shieldCheckIconSvg = string.Empty;

    // PROFILE EDIT
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

    // NOTIFICATIONS
    private bool EmailNotificationsEnabled = true;
    private bool SmsNotificationsEnabled = false;

    // PREFERENCES
    private DynamicModal? CurrencyModal;
    private DynamicModal? LanguageModal;
    private bool IsCurrencyModalOpen = false;
    private bool IsLanguageModalOpen = false;

    private string SelectedCurrency = "NGN";
    private string SelectedLanguage = "English";

    private readonly List<CurrencyOption> AvailableCurrencies = new()
    {
        new CurrencyOption { Code = "NGN", Name = "Nigerian Naira" },
        new CurrencyOption { Code = "USD", Name = "US Dollar" },
        new CurrencyOption { Code = "GBP", Name = "British Pound" },
        new CurrencyOption { Code = "EUR", Name = "Euro" }
    };

    private readonly List<string> AvailableLanguages = new()
    {
        "English", "Hausa", "Yoruba", "Igbo"
    };

    // SECURITY
    private DynamicModal? SecurityModal;
    private bool IsSecurityModalOpen = false;
    private bool IsChangingPassword = false;

    private string CurrentPassword = "";
    private string NewPassword = "";
    private string ConfirmPassword = "";
    private string PasswordChangeError = "";

    // MFA
    private DynamicModal? MfaModal;
    private bool IsMfaModalOpen = false;
    private bool IsMfaEnabled = false;
    private bool IsProcessingMfa = false;
    private int MfaEnrollmentStep = 0;

    private string MfaQrCodeUrl = "";
    private string MfaSecret = "";
    private string MfaVerificationCode = "";
    private string MfaEnrollmentError = "";
    private string? MfaFactorId;

    // LOGOUT
    private ConfirmationPopup? LogoutPopup;
    private bool ShowLogoutPopup = false;

    // DELETE ACCOUNT
    private ConfirmationPopup? DeleteAccountPopup;
    private bool ShowDeleteAccountPopup = false;
    private bool IsDeletingAccount = false;

    // ─── Bucket used for avatar uploads ─────────────────────────────────────────
    // FIX: was "avatars" which was not in the bucket dictionary.
    // "users" maps to the real Supabase bucket "user-avatars".
    private const string AvatarBucketName = "users";

    protected override async Task OnInitializedAsync()
    {
        await LoadSvgsAsync();
        await LoadUserSettings();
    }

    private async Task LoadSvgsAsync()
    {
        try
        {
            profileIconSvg = await VisualElements.GetCustomSvgAsync(
                Domain.Enums.SvgType.User, width: 20, height: 20, fillColor: "var(--primary-color)");

            notificationIconSvg = await VisualElements.GetCustomSvgAsync(
                Domain.Enums.SvgType.Notification, width: 20, height: 20, fillColor: "var(--primary-color)");

            emailIconSvg = await VisualElements.GetCustomSvgAsync(
                Domain.Enums.SvgType.Mail, width: 18, height: 18, fillColor: "var(--text-secondary)");

            smsIconSvg = VisualElements.GenerateSvg(
                "<path fill='var(--text-secondary)' d='M20 2H4c-1.1 0-2 .9-2 2v18l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm0 14H6l-2 2V4h16v12z'/>",
                18, 18, "0 0 24 24");

            settingsIconSvg = await VisualElements.GetCustomSvgAsync(
                Domain.Enums.SvgType.Settings, width: 20, height: 20, fillColor: "var(--primary-color)");

            currencyIconSvg = VisualElements.GenerateSvg(
                "<path fill='var(--text-secondary)' d='M11.8 10.9c-2.27-.59-3-1.2-3-2.15 0-1.09 1.01-1.85 2.7-1.85 1.78 0 2.44.85 2.5 2.1h2.21c-.07-1.72-1.12-3.3-3.21-3.81V3h-3v2.16c-1.94.42-3.5 1.68-3.5 3.61 0 2.31 1.91 3.46 4.7 4.13 2.5.6 3 1.48 3 2.41 0 .69-.49 1.79-2.7 1.79-2.06 0-2.87-.92-2.98-2.1h-2.2c.12 2.19 1.76 3.42 3.68 3.83V21h3v-2.15c1.95-.37 3.5-1.5 3.5-3.55 0-2.84-2.43-3.81-4.7-4.4z'/>",
                18, 18, "0 0 24 24");

            languageIconSvg = VisualElements.GenerateSvg(
                "<path fill='var(--text-secondary)' d='M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zm6.93 6h-2.95c-.32-1.25-.78-2.45-1.38-3.56 1.84.63 3.37 1.91 4.33 3.56zM12 4.04c.83 1.2 1.48 2.53 1.91 3.96h-3.82c.43-1.43 1.08-2.76 1.91-3.96zM4.26 14C4.1 13.36 4 12.69 4 12s.1-1.36.26-2h3.38c-.08.66-.14 1.32-.14 2 0 .68.06 1.34.14 2H4.26zm.82 2h2.95c.32 1.25.78 2.45 1.38 3.56-1.84-.63-3.37-1.9-4.33-3.56zm2.95-8H5.08c.96-1.66 2.49-2.93 4.33-3.56C8.81 5.55 8.35 6.75 8.03 8zM12 19.96c-.83-1.2-1.48-2.53-1.91-3.96h3.82c-.43 1.43-1.08 2.76-1.91 3.96zM14.34 14H9.66c-.09-.66-.16-1.32-.16-2 0-.68.07-1.35.16-2h4.68c.09.65.16 1.32.16 2 0 .68-.07 1.34-.16 2zm.25 5.56c.6-1.11 1.06-2.31 1.38-3.56h2.95c-.96 1.65-2.49 2.93-4.33 3.56zM16.36 14c.08-.66.14-1.32.14-2 0-.68-.06-1.34-.14-2h3.38c.16.64.26 1.31.26 2s-.1 1.36-.26 2h-3.38z'/>",
                18, 18, "0 0 24 24");

            securityIconSvg = VisualElements.GenerateSvg(
                "<path fill='var(--primary-color)' d='M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4z'/>",
                20, 20, "0 0 24 24");

            keyIconSvg = VisualElements.GenerateSvg(
                "<path fill='var(--text-secondary)' d='M12.65 10C11.7 7.31 8.9 5.5 5.77 6.12c-2.29.46-4.15 2.29-4.63 4.58C.32 14.57 3.26 18 7 18c2.61 0 4.83-1.67 5.65-4H17v2c0 1.1.9 2 2 2s2-.9 2-2v-2c1.1 0 2-.9 2-2s-.9-2-2-2h-8.35zM7 14c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2z'/>",
                18, 18, "0 0 24 24");

            mfaIconSvg = VisualElements.GenerateSvg(
                "<path fill='var(--text-secondary)' d='M17 8h-1V6c0-2.76-2.24-5-5-5S6 3.24 6 6v2H5c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V10c0-1.1-.9-2-2-2zM8.9 6c0-1.71 1.39-3.1 3.1-3.1s3.1 1.39 3.1 3.1v2H8.9V6zM17 20H5V10h12v10zm-6-3c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2z'/>",
                18, 18, "0 0 24 24");

            infoIconSvg = VisualElements.GenerateSvg(
                "<path fill='var(--primary-color)' d='M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z'/>",
                20, 20, "0 0 24 24");

            downloadIconSvg = VisualElements.GenerateSvg(
                "<path fill='var(--text-secondary)' d='M19 12v7H5v-7H3v7c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2v-7h-2zm-6 .67l2.59-2.58L17 11.5l-5 5-5-5 1.41-1.41L11 12.67V3h2v9.67z'/>",
                18, 18, "0 0 24 24");

            deleteIconSvg = VisualElements.GenerateSvg(
                "<path fill='#dc3545' d='M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z'/>",
                18, 18, "0 0 24 24");

            logoutIconSvg = VisualElements.GenerateSvg(
                "<path fill='#dc3545' d='M17 7l-1.41 1.41L18.17 11H8v2h10.17l-2.58 2.58L17 17l5-5zM4 5h8V3H4c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h8v-2H4V5z'/>",
                18, 18, "0 0 24 24");

            arrowRightSvg = VisualElements.GenerateSvg(
                "<path fill='var(--text-tertiary)' d='M10 6L8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z'/>",
                16, 16, "0 0 24 24");

            checkIconSvg = VisualElements.GenerateSvg(
                "<path fill='var(--primary-color)' d='M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z'/>",
                20, 20, "0 0 24 24");

            warningIconSvg = VisualElements.GenerateSvg(
                "<path fill='#dc3545' d='M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z'/>",
                20, 20, "0 0 24 24");

            cameraIconSvg = VisualElements.GenerateSvg(
                "<path fill='currentColor' d='M12 15.2c1.77 0 3.2-1.43 3.2-3.2s-1.43-3.2-3.2-3.2-3.2 1.43-3.2 3.2 1.43 3.2 3.2 3.2zm0-5.4c1.21 0 2.2.99 2.2 2.2s-.99 2.2-2.2 2.2-2.2-.99-2.2-2.2.99-2.2 2.2-2.2z'/><path fill='currentColor' d='M9 2L7.17 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2h-3.17L15 2H9zm11 15H4V6h4.05l1.83-2h4.24l1.83 2H20v11z'/>",
                20, 20, "0 0 24 24");

            shieldCheckIconSvg = VisualElements.GenerateSvg(
                "<path fill='var(--primary-color)' d='M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4zm-2 16l-4-4 1.41-1.41L10 14.17l6.59-6.59L18 9l-8 8z'/>",
                48, 48, "0 0 24 24");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading SVGs");
        }
    }

    private async Task LoadUserSettings()
    {
        IsLoading = true;
        StateHasChanged();

        try
        {
            CurrentUserId = await PermissionService.GetCurrentUserIdAsync();

            if (string.IsNullOrEmpty(CurrentUserId))
            {
                Navigation.NavigateTo("signin");
                return;
            }

            UserProfile = await UserService.GetUserByIdAsync(CurrentUserId);

            if (UserProfile == null)
            {
                Navigation.NavigateTo("");
                return;
            }

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

            await CheckPasswordChangeCapability();
            await CheckMfaStatus();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Settings loaded for user: {UserProfile.Email}", LogLevel.Info);
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

    private async Task CheckPasswordChangeCapability()
    {
        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            var providersClaim = user?.FindFirst("app_metadata.providers")?.Value
                ?? user?.FindFirst("providers")?.Value;

            if (!string.IsNullOrEmpty(providersClaim))
                CanChangePassword = !providersClaim.Contains("google");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Checking password change capability");
        }
    }

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
            if (file == null) return;

            if (!file.ContentType.StartsWith("image/"))
            {
                await JSRuntime.InvokeVoidAsync("alert", "Please select an image file");
                return;
            }

            const long maxFileSize = 1 * 1024 * 1024; // 1 MB
            if (file.Size > maxFileSize)
            {
                await JSRuntime.InvokeVoidAsync("alert", "Image must be less than 1MB");
                return;
            }

            IsUploadingAvatar = true;
            StateHasChanged();

            // FIX: Delete the old avatar from storage before uploading a new one
            // to avoid accumulating orphaned files.
            var existingAvatarUrl = UserProfile?.AvatarUrl;
            if (!string.IsNullOrEmpty(existingAvatarUrl))
            {
                await DeleteExistingAvatarAsync(existingAvatarUrl);
            }

            // FIX: Use AvatarBucketName constant ("users") which maps to "user-avatars".
            var result = await StorageService.UploadImageAsync(
                file,
                bucketName: AvatarBucketName,
                folder: $"users/{CurrentUserId}",
                enableCompression: true
            );

            if (result.Success && !string.IsNullOrEmpty(result.PublicUrl))
            {
                TempAvatarUrl = result.PublicUrl;
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Avatar uploaded successfully: {result.PublicUrl}", LogLevel.Info);
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

    /// <summary>
    /// Deletes a previously uploaded avatar from Supabase Storage.
    /// Extracts the relative file path from the public URL and calls DeleteImageAsync.
    /// Failures are logged but do not block the upload of the new avatar.
    /// </summary>
    private async Task DeleteExistingAvatarAsync(string publicUrl)
    {
        try
        {
            // Cast to the concrete type to access ExtractFilePathFromUrl
            if (StorageService is SupabaseStorageService concreteStorage)
            {
                var filePath = concreteStorage.ExtractFilePathFromUrl(publicUrl, AvatarBucketName);
                if (!string.IsNullOrEmpty(filePath))
                {
                    var deleted = await StorageService.DeleteImageAsync(filePath, AvatarBucketName);
                    if (deleted)
                        await MID_HelperFunctions.DebugMessageAsync(
                            $"Old avatar deleted: {filePath}", LogLevel.Info);
                    else
                        await MID_HelperFunctions.DebugMessageAsync(
                            $"Old avatar could not be deleted: {filePath}", LogLevel.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            // Non-fatal — log and continue with the upload
            await MID_HelperFunctions.LogExceptionAsync(ex, "Deleting old avatar");
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

    private async Task HandleEmailNotificationToggle(ChangeEventArgs e)
    {
        try
        {
            var enabled = (bool)(e.Value ?? false);
            var success = await UserService.UpdateUserProfileAsync(CurrentUserId!,
                new UpdateUserRequest { EmailNotifications = enabled });

            if (success)
                EmailNotificationsEnabled = enabled;
            else
            {
                EmailNotificationsEnabled = !enabled;
                StateHasChanged();
                await JSRuntime.InvokeVoidAsync("alert", "Failed to update notification settings");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Toggling email notifications");
        }
    }

    private async Task HandleSmsNotificationToggle(ChangeEventArgs e)
    {
        try
        {
            var enabled = (bool)(e.Value ?? false);
            var success = await UserService.UpdateUserProfileAsync(CurrentUserId!,
                new UpdateUserRequest { SmsNotifications = enabled });

            if (success)
                SmsNotificationsEnabled = enabled;
            else
            {
                SmsNotificationsEnabled = !enabled;
                StateHasChanged();
                await JSRuntime.InvokeVoidAsync("alert", "Failed to update notification settings");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Toggling SMS notifications");
        }
    }

    private void OpenCurrencyModal() { IsCurrencyModalOpen = true; StateHasChanged(); }
    private void CloseCurrencyModal() { IsCurrencyModalOpen = false; StateHasChanged(); }

    private async Task SelectCurrency(string currencyCode)
    {
        try
        {
            var success = await UserService.UpdateUserProfileAsync(CurrentUserId!,
                new UpdateUserRequest { Currency = currencyCode });

            if (success) SelectedCurrency = currencyCode;
            else await JSRuntime.InvokeVoidAsync("alert", "Failed to update currency");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Selecting currency");
        }
        finally { CloseCurrencyModal(); }
    }

    private void OpenLanguageModal() { IsLanguageModalOpen = true; StateHasChanged(); }
    private void CloseLanguageModal() { IsLanguageModalOpen = false; StateHasChanged(); }

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

            var success = await UserService.UpdateUserProfileAsync(CurrentUserId!,
                new UpdateUserRequest { PreferredLanguage = languageCode });

            if (success) SelectedLanguage = language;
            else await JSRuntime.InvokeVoidAsync("alert", "Failed to update language");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Selecting language");
        }
        finally { CloseLanguageModal(); }
    }

    private void OpenSecurityModal()
    {
        if (!CanChangePassword) return;
        CurrentPassword = ""; NewPassword = ""; ConfirmPassword = ""; PasswordChangeError = "";
        IsSecurityModalOpen = true;
        StateHasChanged();
    }

    private void CloseSecurityModal() { IsSecurityModalOpen = false; StateHasChanged(); }

    private async Task ChangePassword()
    {
        try
        {
            PasswordChangeError = "";

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

    private async Task CheckMfaStatus()
    {
        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            var aalClaim = user?.FindFirst("aal")?.Value;
            IsMfaEnabled = aalClaim == "aal2";

            await MID_HelperFunctions.DebugMessageAsync(
                $"MFA Status: {(IsMfaEnabled ? "Enabled (aal2)" : "Disabled (aal1)")}", LogLevel.Info);

            var factors = await AuthService.GetMfaFactorsAsync();
            if (factors != null && factors.Any())
            {
                IsMfaEnabled = true;
                MfaFactorId = factors.FirstOrDefault()?.Id;
                await MID_HelperFunctions.DebugMessageAsync(
                    $"Found {factors.Count} MFA factor(s) enrolled", LogLevel.Info);
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

            await MID_HelperFunctions.DebugMessageAsync("Starting MFA enrollment...", LogLevel.Info);

            var enrollResult = await AuthService.EnrollMfaAsync("totp");

            if (enrollResult.Success && enrollResult.QrCodeUrl != null && enrollResult.Secret != null)
            {
                MfaQrCodeUrl = enrollResult.QrCodeUrl;
                MfaSecret = enrollResult.Secret;
                MfaFactorId = enrollResult.FactorId;
                MfaEnrollmentStep = 1;

                await MID_HelperFunctions.DebugMessageAsync(
                    $"MFA enrollment initiated. Factor ID: {MfaFactorId}", LogLevel.Info);
            }
            else
            {
                MfaEnrollmentError = enrollResult.ErrorMessage ?? "Failed to start MFA enrollment";
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

            var verifyResult = await AuthService.VerifyMfaAsync(MfaFactorId, MfaVerificationCode);

            if (verifyResult.Success)
            {
                IsMfaEnabled = true;
                await JSRuntime.InvokeVoidAsync("alert",
                    "Two-Factor Authentication enabled successfully!");
                CloseMfaModal();
            }
            else
            {
                MfaEnrollmentError = verifyResult.Message ?? "Invalid verification code";
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
                "Are you sure you want to disable Two-Factor Authentication?");
            if (!confirmed) return;

            IsProcessingMfa = true;
            StateHasChanged();

            var factors = await AuthService.GetMfaFactorsAsync();
            if (factors != null && factors.Any())
            {
                foreach (var factor in factors)
                {
                    var result = await AuthService.UnenrollMfaAsync(factor.Id);
                    if (!result.Success)
                    {
                        await JSRuntime.InvokeVoidAsync("alert", $"Failed to disable MFA: {result.Message}");
                        return;
                    }
                }

                IsMfaEnabled = false;
                await JSRuntime.InvokeVoidAsync("alert", "Two-Factor Authentication has been disabled.");
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

    private async Task ExportMyData()
    {
        try
        {
            if (UserProfile == null)
            {
                await JSRuntime.InvokeVoidAsync("alert", "Unable to export data.");
                return;
            }

            var exportData = new
            {
                ExportDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                UserProfile = new
                {
                    UserProfile.Id, UserProfile.Email,
                    UserProfile.FirstName, UserProfile.LastName,
                    UserProfile.Nickname, UserProfile.PhoneNumber,
                    UserProfile.DateOfBirth, UserProfile.Gender,
                    UserProfile.AccountStatus, UserProfile.MemberSince,
                    UserProfile.LastLoginAt
                },
                Statistics = new
                {
                    UserProfile.TotalOrders, UserProfile.TotalSpent,
                    UserProfile.LoyaltyPoints, UserProfile.MembershipTier
                },
                Preferences = new
                {
                    UserProfile.EmailNotifications, UserProfile.SmsNotifications,
                    UserProfile.PreferredLanguage, UserProfile.Currency
                },
                Security = new
                {
                    UserProfile.IsEmailVerified, UserProfile.IsPhoneVerified,
                    MfaEnabled = IsMfaEnabled
                }
            };

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            var fileName = $"SubashaVentures_UserData_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

            await JSRuntime.InvokeVoidAsync("eval", $@"
                const link = document.createElement('a');
                link.href = 'data:application/json;base64,{base64}';
                link.download = '{fileName}';
                link.click();
            ");

            await JSRuntime.InvokeVoidAsync("alert", "Your data has been exported successfully!");
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Exporting user data");
            await JSRuntime.InvokeVoidAsync("alert", "Failed to export data. Please try again.");
        }
    }

    private void ShowDeleteAccountConfirmation() { ShowDeleteAccountPopup = true; StateHasChanged(); }
    private void CancelDeleteAccount() { ShowDeleteAccountPopup = false; StateHasChanged(); }

    private async Task ConfirmDeleteAccount()
    {
        try
        {
            ShowDeleteAccountPopup = false;
            IsDeletingAccount = true;
            StateHasChanged();

            var success = await UserService.DeleteUserAsync(CurrentUserId!);
            if (success)
            {
                await JSRuntime.InvokeVoidAsync("alert",
                    "Your account has been deleted. You will now be logged out.");
                await AuthService.SignOutAsync();
                Navigation.NavigateTo("", forceLoad: true);
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("alert", "Failed to delete account. Please try again.");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Deleting account");
            await JSRuntime.InvokeVoidAsync("alert", "An error occurred. Please try again.");
        }
        finally
        {
            IsDeletingAccount = false;
            StateHasChanged();
        }
    }

    private void ShowLogoutConfirmation() { ShowLogoutPopup = true; StateHasChanged(); }
    private void CancelLogout() { ShowLogoutPopup = false; StateHasChanged(); }

    private async Task ConfirmLogout()
    {
        try
        {
            ShowLogoutPopup = false;
            StateHasChanged();
            await AuthService.SignOutAsync();
            Navigation.NavigateTo("", forceLoad: true);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Logging out");
            await JSRuntime.InvokeVoidAsync("alert", "Failed to logout. Please try again.");
        }
    }

    private string GetMembershipBadgeClass() => UserProfile?.MembershipTier switch
    {
        MembershipTier.Platinum => "platinum",
        MembershipTier.Gold => "gold",
        MembershipTier.Silver => "silver",
        MembershipTier.Bronze => "bronze",
        _ => "secondary"
    };
}

public class CurrencyOption
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}
