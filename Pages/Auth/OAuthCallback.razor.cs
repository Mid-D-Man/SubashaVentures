// Pages/Auth/OAuthCallback.razor.cs - COMPLETE
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SubashaVentures.Services.Auth;
using SubashaVentures.Services.Supabase;
using SubashaVentures.Services.Users;
using SubashaVentures.Services.Storage;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Auth;

public partial class OAuthCallback : ComponentBase
{
    private string statusMessage = "Completing sign in...";
    private string detailMessage = "Please wait while we authenticate your account.";
    private bool hasError = false;
    private bool isProcessing = true;
    private bool isComplete = false;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"📍 OAuth callback page loaded at: {NavigationManager.Uri}",
                LogLevel.Info
            );

            // Update status
            statusMessage = "Processing authentication...";
            detailMessage = "Exchanging authorization code for session.";
            StateHasChanged();

            // ✅ Small delay to ensure page is fully loaded and localStorage is accessible
            await Task.Delay(500);

            // Handle the OAuth callback
            var result = await AuthService.HandleOAuthCallbackAsync();

            if (result.Success)
            {
                isProcessing = false;
                isComplete = true;
                statusMessage = "Sign in successful!";
                detailMessage = "Redirecting you now...";
                StateHasChanged();

                await MID_HelperFunctions.DebugMessageAsync(
                    "✅ OAuth authentication successful",
                    LogLevel.Info
                );

                // Notify auth state changed
                if (AuthStateProvider is SupabaseAuthStateProvider provider)
                {
                    provider.NotifyAuthenticationStateChanged();
                }

                // Wait a moment to show success message
                await Task.Delay(1000);

                // ✅ ROLE-BASED REDIRECT (ONE TIME ONLY)
                var destination = await DetermineRedirectDestinationAsync();

                await MID_HelperFunctions.DebugMessageAsync(
                    $"🔀 Redirecting to: {destination}",
                    LogLevel.Info
                );

                NavigationManager.NavigateTo(destination, forceLoad: true);
            }
            else
            {
                // ✅ BETTER ERROR HANDLING
                hasError = true;
                isProcessing = false;
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"❌ OAuth callback failed: {result.ErrorCode} - {result.Message}",
                    LogLevel.Error
                );
                
                // Provide specific error messages based on error code
                switch (result.ErrorCode)
                {
                    case "OAUTH_NO_VERIFIER":
                        statusMessage = "Session Issue";
                        detailMessage = "We couldn't retrieve your authentication session. This can happen if you closed the browser during sign-in. Please try again.";
                        
                        await MID_HelperFunctions.DebugMessageAsync(
                            "⚠️ PKCE verifier lost - possible localStorage timing issue or browser restriction",
                            LogLevel.Warning
                        );
                        break;
                    
                    case "OAUTH_NO_CODE":
                        statusMessage = "Authorization Failed";
                        detailMessage = "No authorization code received from Google. Please try signing in again.";
                        break;
                    
                    case "OAUTH_EXCHANGE_FAILED":
                        statusMessage = "Session Creation Failed";
                        detailMessage = "We couldn't create your session. Please try signing in again.";
                        break;
                    
                    default:
                        statusMessage = "Authentication Failed";
                        detailMessage = result.Message ?? "An unexpected error occurred. Please try signing in again.";
                        break;
                }
                
                StateHasChanged();

                // Wait before redirect
                await Task.Delay(4000);
                NavigationManager.NavigateTo("signin", forceLoad: false);
            }
        }
        catch (Exception ex)
        {
            hasError = true;
            isProcessing = false;
            statusMessage = "An Error Occurred";
            detailMessage = "Something went wrong during authentication. Please try signing in again.";
            StateHasChanged();

            await MID_HelperFunctions.LogExceptionAsync(ex, "OAuth callback page");

            await Task.Delay(4000);
            NavigationManager.NavigateTo("signin", forceLoad: false);
        }
    }

    /// <summary>
    /// Determine redirect destination based on user role
    /// </summary>
    private async Task<string> DetermineRedirectDestinationAsync()
{
    try
    {
        await MID_HelperFunctions.DebugMessageAsync(
            "🎯 Determining redirect destination...",
            LogLevel.Info
        );

        // Check if there's a stored return URL
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "🔍 Checking for stored return URL...",
                LogLevel.Info
            );

            var returnUrl = await LocalStorage.GetItemAsync<string>("oauth_return_url");
            await LocalStorage.RemoveItemAsync("oauth_return_url");

            if (!string.IsNullOrEmpty(returnUrl))
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"📍 Using stored return URL: {returnUrl}",
                    LogLevel.Info
                );
                return returnUrl;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                "ℹ️ No stored return URL found",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Checking return URL");
        }

        // Get current user to check role
        await MID_HelperFunctions.DebugMessageAsync(
            "👤 Getting current user...",
            LogLevel.Info
        );

        var user = await AuthService.GetCurrentUserAsync();
        
        if (user == null)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "⚠️ No current user found, redirecting to home",
                LogLevel.Warning
            );
            return "";
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"✅ Current user: {user.Email}",
            LogLevel.Info
        );

        // Get user profile with roles
        await MID_HelperFunctions.DebugMessageAsync(
            $"🔍 Fetching user profile for: {user.Id}",
            LogLevel.Info
        );

        var userProfile = await UserService.GetUserByIdAsync(user.Id);
        
        if (userProfile == null)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "⚠️ User profile not found, redirecting to home",
                LogLevel.Warning
            );
            return "";
        }

        await MID_HelperFunctions.DebugMessageAsync(
            $"✅ User profile found. Role: {userProfile.Role}, IsSuperiorAdmin: {userProfile.IsSuperiorAdmin}",
            LogLevel.Info
        );

        // Check if user is superior admin
        if (userProfile.IsSuperiorAdmin)
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"👑 Superior admin detected: {user.Email}, redirecting to admin panel",
                LogLevel.Info
            );
            return "admin";
        }

        // Regular user - go to home
        await MID_HelperFunctions.DebugMessageAsync(
            $"👤 Regular user detected: {user.Email}, redirecting to home",
            LogLevel.Info
        );
        
        return "";
    }
    catch (Exception ex)
    {
        await MID_HelperFunctions.LogExceptionAsync(ex, "Determining redirect destination");
        //_logger.LogError(ex, "Error determining redirect destination");
        return "";
    }
}

    /// <summary>
    /// Retry sign-in on error
    /// </summary>
    private void RetrySignIn()
    {
        NavigationManager.NavigateTo("signin", forceLoad: false);
    }
}
