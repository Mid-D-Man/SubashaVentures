// Pages/User/Partner/PartnerStore.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using SubashaVentures.Domain.Partner;
using SubashaVentures.Services.Partners;
using SubashaVentures.Services.Storage;
using SubashaVentures.Services.Users;
using SubashaVentures.Components.Shared.Notifications;

namespace SubashaVentures.Pages.User.Partner;

public partial class PartnerStore : ComponentBase
{
    [Inject] private IPartnerStoreService        PartnerStoreService { get; set; } = default!;
    [Inject] private ICloudflareR2Service        R2Service           { get; set; } = default!;
    [Inject] private IUserService                UserService         { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider   { get; set; } = default!;
    [Inject] private NavigationManager           Navigation          { get; set; } = default!;

    private bool   isLoading         = true;
    private bool   isSaving          = false;
    private bool   isUploadingLogo   = false;
    private bool   isUploadingBanner = false;
    private string userId            = string.Empty;
    private string partnerId         = string.Empty;
    private string storeId           = string.Empty;

    private PartnerStoreViewModel?     store  = null;
    private StoreFormData              form   = new();
    private Dictionary<string, string> errors = new();

    private NotificationComponent? notificationComponent;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var user      = authState.User;

            if (user.Identity?.IsAuthenticated != true)
            {
                Navigation.NavigateTo("signin");
                return;
            }

            userId    = user.FindFirst("sub")?.Value ?? user.FindFirst("id")?.Value ?? string.Empty;
            partnerId = user.FindFirst("partner_id")?.Value ?? string.Empty;

            if (string.IsNullOrEmpty(partnerId))
            {
                try
                {
                    var dbProfile = await UserService.GetUserByIdAsync(userId);
                    if (dbProfile?.IsPartner == true)
                        partnerId = dbProfile.PartnerId ?? string.Empty;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"PartnerStore DB partnerId fallback: {ex.Message}");
                }
            }

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(partnerId))
            {
                Navigation.NavigateTo("user/partner/dashboard");
                return;
            }

            await LoadStore();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PartnerStore init error: {ex.Message}");
            isLoading = false;
        }
    }

    private async Task LoadStore()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            store = await PartnerStoreService.GetStoreByPartnerIdAsync(partnerId);

            if (store != null)
            {
                storeId = store.Id;
                MapStoreToForm();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PartnerStore load error: {ex.Message}");
            store = null;
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private void MapStoreToForm()
    {
        if (store == null) return;
        form = new StoreFormData
        {
            StoreName   = store.StoreName,
            Tagline     = store.Tagline     ?? string.Empty,
            Description = store.Description ?? string.Empty,
            LogoUrl     = store.LogoUrl     ?? string.Empty,
            BannerUrl   = store.BannerUrl   ?? string.Empty,
            PublicPhone = store.PublicPhone  ?? string.Empty,
            PublicEmail = store.PublicEmail  ?? string.Empty,
        };
    }

    private void ResetForm() => MapStoreToForm();

    // ── Logo Upload ────────────────────────────────────────────────────────────

    private async Task HandleLogoUpload(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file == null) return;

        var validation = R2Service.ValidateImageFile(file);
        if (!validation.IsValid)
        {
            notificationComponent?.ShowWarning(validation.Errors.First());
            return;
        }

        isUploadingLogo = true;
        StateHasChanged();

        try
        {
            // Key is always partners/{id}/store/logo.webp
            var objectKey = R2Service.BuildStoreLogoKey(partnerId);

            // UploadImageAsync handles compression + WebP conversion
            var result = await R2Service.UploadImageAsync(file, objectKey);

            if (result.Success && !string.IsNullOrEmpty(result.PublicUrl))
            {
                form.LogoUrl = result.PublicUrl;
                notificationComponent?.ShowSuccess("Logo uploaded and converted to WebP ✓");
            }
            else
            {
                notificationComponent?.ShowError($"Logo upload failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Upload error: {ex.Message}");
        }
        finally
        {
            isUploadingLogo = false;
            StateHasChanged();
        }
    }

    // ── Banner Upload ──────────────────────────────────────────────────────────

    private async Task HandleBannerUpload(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file == null) return;

        // Banners allow 5 MB
        var validation = R2Service.ValidateImageFile(file, 5_242_880);
        if (!validation.IsValid)
        {
            notificationComponent?.ShowWarning(validation.Errors.First());
            return;
        }

        isUploadingBanner = true;
        StateHasChanged();

        try
        {
            var objectKey = R2Service.BuildStoreBannerKey(partnerId);
            var result    = await R2Service.UploadImageAsync(file, objectKey, 5_242_880);

            if (result.Success && !string.IsNullOrEmpty(result.PublicUrl))
            {
                form.BannerUrl = result.PublicUrl;
                notificationComponent?.ShowSuccess("Banner uploaded and converted to WebP ✓");
            }
            else
            {
                notificationComponent?.ShowError($"Banner upload failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Upload error: {ex.Message}");
        }
        finally
        {
            isUploadingBanner = false;
            StateHasChanged();
        }
    }

    private void RemoveBanner()
    {
        form.BannerUrl = string.Empty;
        StateHasChanged();
    }

    // ── Save ───────────────────────────────────────────────────────────────────

    private async Task HandleSave()
    {
        errors.Clear();

        if (string.IsNullOrWhiteSpace(form.StoreName) || form.StoreName.Trim().Length < 2)
            errors["StoreName"] = "Store name is required";

        if (errors.Any()) { StateHasChanged(); return; }

        isSaving = true;
        StateHasChanged();

        try
        {
            var request = new UpdateStoreRequest
            {
                StoreName   = form.StoreName.Trim(),
                Tagline     = string.IsNullOrWhiteSpace(form.Tagline)     ? null : form.Tagline.Trim(),
                Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim(),
                LogoUrl     = string.IsNullOrWhiteSpace(form.LogoUrl)     ? null : form.LogoUrl,
                BannerUrl   = string.IsNullOrWhiteSpace(form.BannerUrl)   ? null : form.BannerUrl,
                PublicPhone = string.IsNullOrWhiteSpace(form.PublicPhone) ? null : form.PublicPhone.Trim(),
                PublicEmail = string.IsNullOrWhiteSpace(form.PublicEmail) ? null : form.PublicEmail.Trim(),
            };

            var success = await PartnerStoreService.UpdateStoreAsync(storeId, partnerId, request);

            if (success)
            {
                notificationComponent?.ShowSuccess("Store profile saved ✓");
                await LoadStore();
            }
            else
            {
                notificationComponent?.ShowError("Failed to save. Please try again.");
            }
        }
        catch (Exception ex)
        {
            notificationComponent?.ShowError($"Error: {ex.Message}");
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    private bool   HasError(string field) => errors.ContainsKey(field);
    private string GetError(string field) => errors.GetValueOrDefault(field, string.Empty);

    public class StoreFormData
    {
        public string StoreName   { get; set; } = string.Empty;
        public string Tagline     { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string LogoUrl     { get; set; } = string.Empty;
        public string BannerUrl   { get; set; } = string.Empty;
        public string PublicPhone { get; set; } = string.Empty;
        public string PublicEmail { get; set; } = string.Empty;
    }
}
