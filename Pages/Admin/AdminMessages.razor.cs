using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SubashaVentures.Services.Firebase;
using SubashaVentures.Services.Users;
using SubashaVentures.Services.Authorization;
using SubashaVentures.Services.VisualElements;
using SubashaVentures.Components.Shared.Modals;
using SubashaVentures.Models.Firebase;
using SubashaVentures.Domain.Enums;
using SubashaVentures.Domain.User;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Pages.Admin;

public partial class AdminMessages : ComponentBase
{
    [Inject] private IMessagingService MessagingService { get; set; } = default!;
    [Inject] private IUserSegmentationService SegmentationService { get; set; } = default!;
    [Inject] private IUserService UserService { get; set; } = default!;
    [Inject] private IPermissionService PermissionService { get; set; } = default!;
    [Inject] private IVisualElementsService VisualElements { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ILogger<AdminMessages> Logger { get; set; } = default!;

    private DynamicModal NewMessageModal { get; set; } = default!;
    private DynamicModal BulkMessageModal { get; set; } = default!;
    private DynamicModal ConversationModal { get; set; } = default!;

    private List<AdminInboxModel> AllConversations { get; set; } = new();
    private List<AdminInboxModel> FilteredConversations { get; set; } = new();
    private List<MessageModel> ConversationMessages { get; set; } = new();
    private AdminInboxModel? SelectedConversation { get; set; }
    
    private string SelectedStatus { get; set; } = "";
    private string SelectedCategory { get; set; } = "";
    private string SearchQuery { get; set; } = "";
    private string ReplyMessage { get; set; } = "";
    
    private int TotalConversations { get; set; }
    private int UnreadCount { get; set; }
    private int ActiveUsersCount { get; set; }
    private int TargetUserCount { get; set; }
    
    private bool IsLoading { get; set; }
    private bool IsSendingMessage { get; set; }
    private bool IsSendingBulkMessage { get; set; }
    private bool IsSendingReply { get; set; }
    private bool IsCalculating { get; set; }

    private NewMessageDto NewMessage { get; set; } = new();
    private BulkMessageDto BulkMessage { get; set; } = new();

    // ✅ ELEMENT REFERENCES FOR SVG INJECTION
    private ElementReference messagesStatIconRef;
    private ElementReference mailStatIconRef;
    private ElementReference userStatIconRef;
    private ElementReference newMessageBtnIconRef;
    private ElementReference bulkMessageBtnIconRef;
    private ElementReference refreshBtnIconRef;
    private ElementReference emptyMessagesIconRef;

    private string SelectedConversationSubject => SelectedConversation?.Subject ?? "Conversation";

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var isSuperiorAdmin = await PermissionService.IsSuperiorAdminAsync();
            if (!isSuperiorAdmin)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    "Unauthorized access attempt to admin messages",
                    LogLevel.Warning
                );
                Navigation.NavigateTo("user");
                return;
            }

            await LoadConversationsAsync();
            await LoadStatisticsAsync();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Initializing admin messages page");
            Logger.LogError(ex, "Failed to initialize admin messages page");
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await LoadIconsAsync();
        }
    }

    // ✅ PROPER ICON LOADING WITH JSRUNTIME INJECTION
    private async Task LoadIconsAsync()
    {
        try
        {
            // Header button icons
            var messagesSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Messages,
                width: 20,
                height: 20,
                fillColor: "currentColor"
            );

            var mailSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Mail,
                width: 20,
                height: 20,
                fillColor: "currentColor"
            );

            var settingsSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Settings,
                width: 20,
                height: 20,
                fillColor: "currentColor"
            );

            // Stats card icons
            var messagesStatSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Messages,
                width: 32,
                height: 32,
                fillColor: "var(--primary-color)"
            );

            var mailStatSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.Mail,
                width: 32,
                height: 32,
                fillColor: "var(--primary-color)"
            );

            var userStatSvg = await VisualElements.GetCustomSvgAsync(
                SvgType.User,
                width: 32,
                height: 32,
                fillColor: "var(--primary-color)"
            );

            // Inject into DOM
            await JSRuntime.InvokeVoidAsync("eval",
                $"if(document.querySelector('.messages-header .header-right button:nth-child(1) svg')) {{}} else if(document.querySelector('.messages-header .header-right button:nth-child(1)')) {{ var btn = document.querySelector('.messages-header .header-right button:nth-child(1)'); var span = btn.querySelector('span'); btn.innerHTML = `{messagesSvg}` + (span ? span.outerHTML : ''); }}");

            await JSRuntime.InvokeVoidAsync("eval",
                $"if(document.querySelector('.messages-header .header-right button:nth-child(2) svg')) {{}} else if(document.querySelector('.messages-header .header-right button:nth-child(2)')) {{ var btn = document.querySelector('.messages-header .header-right button:nth-child(2)'); var span = btn.querySelector('span'); btn.innerHTML = `{mailSvg}` + (span ? span.outerHTML : ''); }}");

            await JSRuntime.InvokeVoidAsync("eval",
                $"if(document.querySelector('.toolbar-actions button svg')) {{}} else if(document.querySelector('.toolbar-actions button')) {{ var btn = document.querySelector('.toolbar-actions button'); var span = btn.querySelector('span'); btn.innerHTML = `{settingsSvg}` + (span ? span.outerHTML : ''); }}");

            // Stats icons
            await JSRuntime.InvokeVoidAsync("eval",
                $"if(document.querySelectorAll('.stat-icon')[0]) document.querySelectorAll('.stat-icon')[0].innerHTML = `{messagesStatSvg}`;");

            await JSRuntime.InvokeVoidAsync("eval",
                $"if(document.querySelectorAll('.stat-icon')[1]) document.querySelectorAll('.stat-icon')[1].innerHTML = `{mailStatSvg}`;");

            await JSRuntime.InvokeVoidAsync("eval",
                $"if(document.querySelectorAll('.stat-icon')[2]) document.querySelectorAll('.stat-icon')[2].innerHTML = `{userStatSvg}`;");

            // Empty state icon
            if (!FilteredConversations.Any())
            {
                var emptyMessagesSvg = await VisualElements.GetCustomSvgAsync(
                    SvgType.Messages,
                    width: 64,
                    height: 64,
                    fillColor: "var(--text-muted)"
                );

                await JSRuntime.InvokeVoidAsync("eval",
                    $"if(document.querySelector('.empty-icon')) document.querySelector('.empty-icon').innerHTML = `{emptyMessagesSvg}`;");
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading admin message icons");
            Logger.LogError(ex, "Failed to load icons");
        }
    }

    private async Task LoadConversationsAsync()
    {
        try
        {
            IsLoading = true;
            StateHasChanged();

            AllConversations = await MessagingService.GetAdminInboxAsync(null, 100);
            FilterConversations();

            await MID_HelperFunctions.DebugMessageAsync(
                $"Loaded {AllConversations.Count} conversations",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading conversations");
            Logger.LogError(ex, "Failed to load conversations");
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadStatisticsAsync()
    {
        try
        {
            TotalConversations = AllConversations.Count;
            UnreadCount = AllConversations.Count(c => c.UnreadByAdmin);
            
            var userStats = await UserService.GetUserStatisticsAsync();
            ActiveUsersCount = userStats.ActiveUsers;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Loading statistics");
            Logger.LogError(ex, "Failed to load statistics");
        }
    }

    private void FilterConversations()
    {
        FilteredConversations = AllConversations;

        if (!string.IsNullOrWhiteSpace(SelectedStatus))
        {
            FilteredConversations = FilteredConversations
                .Where(c => c.Status.Equals(SelectedStatus, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(SelectedCategory))
        {
            FilteredConversations = FilteredConversations
                .Where(c => c.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var query = SearchQuery.ToLowerInvariant();
            FilteredConversations = FilteredConversations
                .Where(c => c.UserEmail.ToLowerInvariant().Contains(query) ||
                           c.Subject.ToLowerInvariant().Contains(query) ||
                           c.UserDisplayName.ToLowerInvariant().Contains(query))
                .ToList();
        }

        FilteredConversations = FilteredConversations
            .OrderByDescending(c => c.LastMessageAt)
            .ToList();
    }

    private async Task RefreshInbox()
    {
        await LoadConversationsAsync();
        await LoadStatisticsAsync();
        StateHasChanged();
        await LoadIconsAsync();
    }

    private void OpenNewMessageModal()
    {
        NewMessage = new NewMessageDto();
        NewMessageModal.Open();
    }

    private void CloseNewMessageModal()
    {
        NewMessageModal.Close();
    }

    private void OpenBulkMessageModal()
    {
        BulkMessage = new BulkMessageDto();
        TargetUserCount = 0;
        BulkMessageModal.Open();
    }

    private void CloseBulkMessageModal()
    {
        BulkMessageModal.Close();
    }

    private async Task OpenConversation(string conversationId)
    {
        try
        {
            SelectedConversation = AllConversations.FirstOrDefault(c => c.Id == conversationId);
            if (SelectedConversation == null) return;

            ReplyMessage = "";
            ConversationMessages = await MessagingService.GetConversationMessagesAsync(
                SelectedConversation.UserId,
                conversationId
            );

            if (SelectedConversation.UnreadByAdmin)
            {
                await MessagingService.MarkAdminReadAsync(conversationId);
                SelectedConversation.UnreadByAdmin = false;
                UnreadCount = Math.Max(0, UnreadCount - 1);
            }

            ConversationModal.Open();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Opening conversation: {conversationId}");
            Logger.LogError(ex, "Failed to open conversation: {ConversationId}", conversationId);
        }
    }

    private async Task SendNewMessage()
    {
        try
        {
            IsSendingMessage = true;
            StateHasChanged();

            var user = await UserService.GetUserByEmailAsync(NewMessage.RecipientIdentifier) ??
                      await UserService.GetUserByIdAsync(NewMessage.RecipientIdentifier);

            if (user == null)
            {
                Logger.LogWarning("User not found: {Identifier}", NewMessage.RecipientIdentifier);
                return;
            }

            var conversationId = await MessagingService.CreateConversationAsync(
                user.Id,
                user.Email,
                NewMessage.Subject,
                NewMessage.Category,
                NewMessage.Priority
            );

            if (string.IsNullOrEmpty(conversationId))
            {
                Logger.LogError("Failed to create conversation");
                return;
            }

            var messageId = await MessagingService.SendMessageAsync(
                user.Id,
                conversationId,
                "admin",
                "Admin",
                NewMessage.MessageText
            );

            if (string.IsNullOrEmpty(messageId))
            {
                Logger.LogError("Failed to send message");
                return;
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Message sent to user: {user.Email}",
                LogLevel.Info
            );

            CloseNewMessageModal();
            await RefreshInbox();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Sending new message");
            Logger.LogError(ex, "Failed to send new message");
        }
        finally
        {
            IsSendingMessage = false;
            StateHasChanged();
        }
    }

    private async Task CalculateTargetUsers()
    {
        try
        {
            IsCalculating = true;
            StateHasChanged();

            var criteria = BuildSegmentationCriteria();
            var userIds = await SegmentationService.GetUserIdsByCriteriaAsync(criteria);
            TargetUserCount = userIds.Count;

            await MID_HelperFunctions.DebugMessageAsync(
                $"Calculated target users: {TargetUserCount}",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Calculating target users");
            Logger.LogError(ex, "Failed to calculate target users");
        }
        finally
        {
            IsCalculating = false;
            StateHasChanged();
        }
    }

    private async Task SendBulkMessage()
    {
        try
        {
            IsSendingBulkMessage = true;
            StateHasChanged();

            var criteria = BuildSegmentationCriteria();
            var userIds = await SegmentationService.GetUserIdsByCriteriaAsync(criteria);

            if (!userIds.Any())
            {
                Logger.LogWarning("No users match the criteria");
                return;
            }

            var expiresAt = BulkMessage.ExpiresAt.HasValue 
                ? BulkMessage.ExpiresAt.Value 
                : (DateTime?)null;

            var results = await MessagingService.SendBulkMessageAsync(
                userIds,
                BulkMessage.Subject,
                BulkMessage.MessageText,
                BulkMessage.Category,
                expiresAt
            );

            var successCount = results.Values.Count(x => x);
            await MID_HelperFunctions.DebugMessageAsync(
                $"Bulk message sent: {successCount}/{userIds.Count} successful",
                LogLevel.Info
            );

            CloseBulkMessageModal();
            await RefreshInbox();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Sending bulk message");
            Logger.LogError(ex, "Failed to send bulk message");
        }
        finally
        {
            IsSendingBulkMessage = false;
            StateHasChanged();
        }
    }

    private async Task SendReply()
    {
        if (SelectedConversation == null || string.IsNullOrWhiteSpace(ReplyMessage))
            return;

        try
        {
            IsSendingReply = true;
            StateHasChanged();

            var messageId = await MessagingService.SendMessageAsync(
                SelectedConversation.UserId,
                SelectedConversation.Id,
                "admin",
                "Admin",
                ReplyMessage
            );

            if (!string.IsNullOrEmpty(messageId))
            {
                var newMessage = new MessageModel
                {
                    Id = messageId,
                    Text = ReplyMessage,
                    SenderId = "admin",
                    SenderName = "Admin",
                    Timestamp = DateTime.UtcNow,
                    IsRead = false
                };

                ConversationMessages.Add(newMessage);
                ReplyMessage = "";

                await MID_HelperFunctions.DebugMessageAsync(
                    "Reply sent successfully",
                    LogLevel.Info
                );
            }
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Sending reply");
            Logger.LogError(ex, "Failed to send reply");
        }
        finally
        {
            IsSendingReply = false;
            StateHasChanged();
        }
    }

    private async Task UpdateConversationStatus()
    {
        if (SelectedConversation == null) return;

        try
        {
            await MessagingService.UpdateConversationStatusAsync(
                SelectedConversation.UserId,
                SelectedConversation.Id,
                SelectedConversation.Status
            );

            await MID_HelperFunctions.DebugMessageAsync(
                $"Conversation status updated: {SelectedConversation.Status}",
                LogLevel.Info
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Updating conversation status");
            Logger.LogError(ex, "Failed to update conversation status");
        }
    }

    private UserSegmentationCriteria BuildSegmentationCriteria()
    {
        var criteria = new UserSegmentationCriteria();

        if (BulkMessage.UseSpendingFilter)
        {
            if (decimal.TryParse(BulkMessage.MinSpent, out var minSpent))
                criteria.MinSpent = minSpent;
            if (decimal.TryParse(BulkMessage.MaxSpent, out var maxSpent))
                criteria.MaxSpent = maxSpent;
        }

        if (BulkMessage.UseLoyaltyFilter)
        {
            if (int.TryParse(BulkMessage.MinLoyaltyPoints, out var minPoints))
                criteria.MinLoyaltyPoints = minPoints;
            if (int.TryParse(BulkMessage.MaxLoyaltyPoints, out var maxPoints))
                criteria.MaxLoyaltyPoints = maxPoints;
        }

        if (BulkMessage.UseMembershipFilter)
        {
            var tiers = new List<MembershipTier>();
            if (BulkMessage.IncludeBronze) tiers.Add(MembershipTier.Bronze);
            if (BulkMessage.IncludeSilver) tiers.Add(MembershipTier.Silver);
            if (BulkMessage.IncludeGold) tiers.Add(MembershipTier.Gold);
            if (BulkMessage.IncludePlatinum) tiers.Add(MembershipTier.Platinum);
            
            if (tiers.Any())
                criteria.MembershipTiers = tiers;
        }

        if (BulkMessage.FilterCartUsers)
            criteria.HasCartItems = true;

        if (BulkMessage.FilterWishlistUsers)
            criteria.HasWishlistItems = true;

        return criteria;
    }

    private bool IsNewMessageValid()
    {
        return !string.IsNullOrWhiteSpace(NewMessage.RecipientIdentifier) &&
               !string.IsNullOrWhiteSpace(NewMessage.Subject) &&
               !string.IsNullOrWhiteSpace(NewMessage.MessageText);
    }

    private bool IsBulkMessageValid()
    {
        return !string.IsNullOrWhiteSpace(BulkMessage.Subject) &&
               !string.IsNullOrWhiteSpace(BulkMessage.MessageText);
    }

    private string GetUserInitials(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return "?";

        var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[1][0]}".ToUpper();
        
        return displayName.Length >= 2 
            ? displayName.Substring(0, 2).ToUpper() 
            : displayName[0].ToString().ToUpper();
    }

    private string FormatTimeAgo(DateTime timestamp)
    {
        var span = DateTime.UtcNow - timestamp;
        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return timestamp.ToString("MMM dd, yyyy");
    }

    private string FormatMessageTime(DateTime timestamp)
    {
        var span = DateTime.UtcNow - timestamp;
        if (span.TotalHours < 24)
            return timestamp.ToString("h:mm tt");
        return timestamp.ToString("MMM dd, h:mm tt");
    }

    private class NewMessageDto
    {
        public string RecipientIdentifier { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Category { get; set; } = "system";
        public string Priority { get; set; } = "normal";
        public string MessageText { get; set; } = "";
        public DateTime? ExpiresAt { get; set; }
    }

    private class BulkMessageDto
    {
        public string Subject { get; set; } = "";
        public string Category { get; set; } = "system";
        public string MessageText { get; set; } = "";
        public DateTime? ExpiresAt { get; set; }
        
        public bool UseSpendingFilter { get; set; }
        public string MinSpent { get; set; } = "";
        public string MaxSpent { get; set; } = "";
        
        public bool UseLoyaltyFilter { get; set; }
        public string MinLoyaltyPoints { get; set; } = "";
        public string MaxLoyaltyPoints { get; set; } = "";
        
        public bool UseMembershipFilter { get; set; }
        public bool IncludeBronze { get; set; }
        public bool IncludeSilver { get; set; }
        public bool IncludeGold { get; set; }
        public bool IncludePlatinum { get; set; }
        
        public bool FilterCartUsers { get; set; }
        public bool FilterWishlistUsers { get; set; }
    }
}
