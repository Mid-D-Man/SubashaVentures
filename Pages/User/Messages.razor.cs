using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Services.VisualElements;
using SubashaVentures.Models.Firebase;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.User;

public partial class Messages : ComponentBase, IDisposable
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IPermissionService PermissionService { get; set; } = default!;
    [Inject] private IMessagingService MessagingService { get; set; } = default!;
    [Inject] private IVisualElementsService VisualElements { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ILogger<Messages> Logger { get; set; } = default!;

    // State
    private bool IsLoading = true;
    private bool IsLoadingMessages = false;
    private bool IsMobileSidebarOpen = false;
    private string CurrentUserId = string.Empty;
    private string ActiveCategory = "all";
    private string? SelectedConversationId = null;
    private int UnreadCount = 0;

    // Data
    private List<ConversationModel> AllConversations = new();
    private List<ConversationModel> FilteredConversations = new();
    private List<MessageModel> CurrentMessages = new();
    private ConversationModel? SelectedConversation = null;

    // References
    private ElementReference MessagesThreadRef;

    // SVG Icons
    private string allIconSvg = string.Empty;
    private string systemIconSvg = string.Empty;
    private string promotionIconSvg = string.Empty;
    private string supportIconSvg = string.Empty;
    private string emptyIconSvg = string.Empty;
    private string selectMessageIconSvg = string.Empty;
    private string emptyMessagesIconSvg = string.Empty;
    private string priorityIconSvg = string.Empty;
    private string backIconSvg = string.Empty;
    private string categoryIconSvg = string.Empty;
    private string statusIconSvg = string.Empty;
    private string timeIconSvg = string.Empty;
    private string adminAvatarSvg = string.Empty;
    private string userAvatarSvg = string.Empty;
    private string attachmentIconSvg = string.Empty;
    private string infoIconSvg = string.Empty;

    // Category-specific icons
    private string systemCategoryIconSvg = string.Empty;
    private string promotionCategoryIconSvg = string.Empty;
    private string supportCategoryIconSvg = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Initializing Messages page",
                LogLevel.Info
            );

            await base.OnInitializedAsync();

            // Load SVGs first
            await LoadSvgIconsAsync();

            // Check authentication
            var isAuthenticated = await PermissionService.IsAuthenticatedAsync();
            if (!isAuthenticated)
            {
                PermissionService.NavigateToSignIn("user/messages");
                return;
            }

            CurrentUserId = await PermissionService.GetCurrentUserIdAsync() ?? string.Empty;

            if (string.IsNullOrEmpty(CurrentUserId))
            {
                Logger.LogWarning("User ID not found after authentication");
                Navigation.NavigateTo("signin");
                return;
            }

            // Load conversations
            await LoadConversationsAsync();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Initializing Messages page");
            Logger.LogError(ex, "Failed to initialize Messages page");
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadSvgIconsAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                "Loading SVG icons for Messages page",
                LogLevel.Info
            );

            // Tab icons (24x24)
            allIconSvg = VisualElements.GenerateSvg(
                "<path d='M3 5h18v2H3V5zm0 6h18v2H3v-2zm0 6h18v2H3v-2z' fill='currentColor'/>",
                24, 24, "0 0 24 24"
            );

            systemIconSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Settings,
                width: 24,
                height: 24
            );

            promotionIconSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Offer,
                width: 24,
                height: 24
            );

            supportIconSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.HelpCenter,
                width: 24,
                height: 24
            );

            // Navigation icons (20x20)
            backIconSvg = VisualElements.GenerateSvg(
                "<path d='M10 6L4 12l6 6' stroke='currentColor' stroke-width='2' fill='none' stroke-linecap='round' stroke-linejoin='round'/>",
                20, 20, "0 0 20 20"
            );

            priorityIconSvg = VisualElements.GenerateSvg(
                "<path d='M12 2L2 22h20L12 2z' fill='currentColor'/>",
                16, 16, "0 0 24 24"
            );

            // Empty state icons (64x64)
            emptyIconSvg = VisualElements.GenerateSvg(
                "<path d='M32 8c-13.2 0-24 8.4-24 19.2 0 6 3.2 11.4 8.4 14.8L14 52l11.2-6.8c2.2.4 4.4.8 6.8.8 13.2 0 24-8.4 24-19.2S45.2 8 32 8z' fill='currentColor' opacity='0.2'/>",
                64, 64, "0 0 64 64"
            );

            selectMessageIconSvg = VisualElements.GenerateSvg(
                "<rect x='8' y='12' width='48' height='8' rx='2' fill='currentColor' opacity='0.2'/><rect x='8' y='28' width='48' height='8' rx='2' fill='currentColor' opacity='0.2'/><rect x='8' y='44' width='32' height='8' rx='2' fill='currentColor' opacity='0.2'/>",
                64, 64, "0 0 64 64"
            );

            emptyMessagesIconSvg = VisualElements.GenerateSvg(
                "<path d='M32 16c-8.8 0-16 5.6-16 12.8 0 4 2.1 7.6 5.6 9.9L20 48l7.5-4.5c1.5.3 2.9.5 4.5.5 8.8 0 16-5.6 16-12.8S40.8 16 32 16z' fill='currentColor' opacity='0.15'/>",
                64, 64, "0 0 64 64"
            );

            // Meta icons (16x16)
            categoryIconSvg = VisualElements.GenerateSvg(
                "<path d='M3 3h4v4H3V3zm6 0h4v4H9V3zm6 0h4v4h-4V3zM3 9h4v4H3V9zm6 0h4v4H9V9zm6 0h4v4h-4V9z' fill='currentColor'/>",
                16, 16, "0 0 18 18"
            );

            statusIconSvg = VisualElements.GenerateSvg(
                "<circle cx='8' cy='8' r='6' fill='none' stroke='currentColor' stroke-width='2'/><path d='M5 8l2 2 4-4' stroke='currentColor' stroke-width='2' fill='none' stroke-linecap='round' stroke-linejoin='round'/>",
                16, 16, "0 0 16 16"
            );

            timeIconSvg = VisualElements.GenerateSvg(
                "<circle cx='8' cy='8' r='6' fill='none' stroke='currentColor' stroke-width='1.5'/><path d='M8 4v4l3 2' stroke='currentColor' stroke-width='1.5' fill='none' stroke-linecap='round'/>",
                16, 16, "0 0 16 16"
            );

            infoIconSvg = VisualElements.GenerateSvg(
                "<circle cx='8' cy='8' r='6' fill='none' stroke='currentColor' stroke-width='1.5'/><path d='M8 6v4M8 11h.01' stroke='currentColor' stroke-width='1.5' stroke-linecap='round'/>",
                16, 16, "0 0 16 16"
            );

            // Avatar icons (32x32)
            adminAvatarSvg = VisualElements.GenerateSvg(
                "<circle cx='16' cy='16' r='14' fill='var(--primary-color)'/><path d='M16 8a4 4 0 100 8 4 4 0 000-8zm-6 14c0-3.3 2.7-6 6-6s6 2.7 6 6' stroke='white' stroke-width='2' fill='none'/>",
                32, 32, "0 0 32 32"
            );

            userAvatarSvg = VisualElements.GenerateSvg(
                "<circle cx='16' cy='16' r='14' fill='var(--gray-400)'/><path d='M16 8a4 4 0 100 8 4 4 0 000-8zm-6 14c0-3.3 2.7-6 6-6s6 2.7 6 6' stroke='white' stroke-width='2' fill='none'/>",
                32, 32, "0 0 32 32"
            );

            // Attachment icon (20x20)
            attachmentIconSvg = VisualElements.GenerateSvg(
                "<path d='M14 3l-8 8a4 4 0 006 6l8-8a2.5 2.5 0 00-3.5-3.5l-7 7a1 1 0 001.5 1.5l6-6' stroke='currentColor' stroke-width='2' fill='none' stroke-linecap='round' stroke-linejoin='round'/>",
                20, 20, "0 0 20 20"
            );

            // Category-specific icons (40x40)
            systemCategoryIconSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Settings,
                width: 40,
                height: 40,
                fillColor: "var(--primary-color)"
            );

            promotionCategoryIconSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Offer,
                width: 40,
                height: 40,
                fillColor: "var(--secondary-color)"
            );

            supportCategoryIconSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.HelpCenter,
                width: 40,
                height: 40,
                fillColor: "var(--info-color)"
            );

            await MID_HelperFunctions.DebugMessageAsync(
                "SVG icons loaded successfully",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading SVG icons");
            Logger.LogError(ex, "Failed to load SVG icons for Messages page");
        }
    }

    private async Task LoadConversationsAsync()
    {
        try
        {
            await MID_HelperFunctions.DebugMessageAsync(
                $"Loading conversations for user: {CurrentUserId}",
                LogLevel.Info
            );

            AllConversations = await MessagingService.GetUserConversationsAsync(CurrentUserId);

            if (AllConversations == null || !AllConversations.Any())
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "No conversations found for user",
                    LogLevel.Info
                );
                AllConversations = new List<ConversationModel>();
            }

            // Calculate unread count
            UnreadCount = AllConversations.Sum(c => c.UnreadCount);

            // Filter conversations
            FilterConversations();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded {AllConversations.Count} conversations, {UnreadCount} unread",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading conversations");
            Logger.LogError(ex, "Failed to load conversations for user: {UserId}", CurrentUserId);
            AllConversations = new List<ConversationModel>();
        }
    }

    private void FilterConversations()
    {
        if (ActiveCategory == "all")
        {
            FilteredConversations = AllConversations.ToList();
        }
        else
        {
            FilteredConversations = AllConversations
                .Where(c => c.Category.Equals(ActiveCategory, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        FilteredConversations = FilteredConversations
            .OrderByDescending(c => c.LastMessageAt)
            .ToList();
    }

    private async Task FilterByCategory(string category)
    {
        ActiveCategory = category;
        FilterConversations();
        StateHasChanged();

        await MID_HelperFunctions.DebugMessageAsync(
            $"Filtered conversations by category: {category}",
            LogLevel.Info
        );
    }

    private async Task SelectConversation(string conversationId)
    {
        try
        {
            if (SelectedConversationId == conversationId)
                return;

            IsLoadingMessages = true;
            SelectedConversationId = conversationId;
            IsMobileSidebarOpen = true;
            StateHasChanged();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Selected conversation: {conversationId}",
                LogLevel.Info
            );

            // Get conversation details
            SelectedConversation = AllConversations.FirstOrDefault(c => c.Id == conversationId);

            // Load messages
            CurrentMessages = await MessagingService.GetConversationMessagesAsync(
                CurrentUserId, 
                conversationId
            );

            if (CurrentMessages == null)
            {
                CurrentMessages = new List<MessageModel>();
            }

            // Sort messages by timestamp
            CurrentMessages = CurrentMessages
                .OrderBy(m => m.Timestamp)
                .ToList();

            IsLoadingMessages = false;
            StateHasChanged();

            // Scroll to bottom of messages
            await Task.Delay(100);
            await ScrollToBottomAsync();

            // Mark unread messages as read
            var unreadMessageIds = CurrentMessages
                .Where(m => !m.IsRead && m.SenderId == "admin")
                .Select(m => m.Id)
                .ToList();

            if (unreadMessageIds.Any())
            {
                var marked = await MessagingService.MarkMessagesAsReadAsync(
                    CurrentUserId,
                    conversationId,
                    unreadMessageIds
                );

                if (marked)
                {
                    // Update local state
                    foreach (var message in CurrentMessages.Where(m => unreadMessageIds.Contains(m.Id)))
                    {
                        message.IsRead = true;
                    }

                    // Update conversation unread count
                    if (SelectedConversation != null)
                    {
                        SelectedConversation.UnreadCount = 0;
                    }

                    // Recalculate total unread count
                    UnreadCount = AllConversations.Sum(c => c.UnreadCount);
                    StateHasChanged();

                    await MID_HelperFunctions.DebugMessageAsync(
                        $"Marked {unreadMessageIds.Count} messages as read",
                        LogLevel.Info
                    );
                }
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Selecting conversation: {conversationId}");
            Logger.LogError(ex, "Failed to load conversation: {ConversationId}", conversationId);
            IsLoadingMessages = false;
            StateHasChanged();
        }
    }

    private async Task ScrollToBottomAsync()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("scrollToBottom", MessagesThreadRef);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to scroll to bottom of messages");
        }
    }

    private void CloseMobileConversation()
    {
        IsMobileSidebarOpen = false;
        StateHasChanged();
    }

    private int GetCategoryCount(string category)
    {
        if (category == "all")
        {
            return AllConversations.Count;
        }

        return AllConversations.Count(c => 
            c.Category.Equals(category, StringComparison.OrdinalIgnoreCase)
        );
    }

    private string GetCategoryIcon(string category)
    {
        return category.ToLowerInvariant() switch
        {
            "system" => systemCategoryIconSvg,
            "promotion" => promotionCategoryIconSvg,
            "support" => supportCategoryIconSvg,
            _ => systemCategoryIconSvg
        };
    }

    private string FormatTimeAgo(DateTime dateTime)
    {
        var span = DateTime.UtcNow - dateTime;

        if (span.TotalMinutes < 1)
            return "Just now";
        if (span.TotalMinutes < 60)
            return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24)
            return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7)
            return $"{(int)span.TotalDays}d ago";
        if (span.TotalDays < 30)
            return $"{(int)(span.TotalDays / 7)}w ago";

        return dateTime.ToString("MMM dd");
    }

    private string FormatMessageTime(DateTime dateTime)
    {
        var today = DateTime.UtcNow.Date;
        var messageDate = dateTime.Date;

        if (messageDate == today)
        {
            return dateTime.ToString("h:mm tt");
        }
        else if (messageDate == today.AddDays(-1))
        {
            return $"Yesterday {dateTime:h:mm tt}";
        }
        else if ((today - messageDate).TotalDays < 7)
        {
            return dateTime.ToString("ddd h:mm tt");
        }
        else
        {
            return dateTime.ToString("MMM dd, h:mm tt");
        }
    }

    private string FormatDate(DateTime dateTime)
    {
        return dateTime.ToString("MMM dd, yyyy");
    }

    public void Dispose()
    {
        AllConversations?.Clear();
        FilteredConversations?.Clear();
        CurrentMessages?.Clear();
    }
}
