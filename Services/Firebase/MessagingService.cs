// Services/Firebase/MessagingService.cs
using SubashaVentures.Models.Firebase;
using SubashaVentures.Utilities.HelperScripts;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Firebase;

public class MessagingService : IMessagingService
{
    private readonly IFirestoreService _firestore;
    private readonly ILogger<MessagingService> _logger;

    public MessagingService(
        IFirestoreService firestore,
        ILogger<MessagingService> logger)
    {
        _firestore = firestore;
        _logger = logger;
    }

    // ==================== CONVERSATION MANAGEMENT ====================

    public async Task<string?> CreateConversationAsync(
        string userId, 
        string userEmail, 
        string subject, 
        string category, 
        string priority = "normal")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(subject))
            {
                _logger.LogWarning("CreateConversation called with invalid parameters");
                return null;
            }

            var conversationId = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;

            var conversation = new ConversationModel
            {
                Id = conversationId,
                Subject = subject,
                Status = "open",
                Priority = priority,
                Category = category,
                CreatedAt = now,
                LastMessageAt = now,
                IsAdminReplied = false,
                UnreadCount = 0
            };

            var userPath = $"messages/{userId}/conversations";
            var conversationSaved = await _firestore.AddDocumentAsync(
                userPath,
                conversation,
                conversationId
            );

            if (string.IsNullOrEmpty(conversationSaved))
            {
                _logger.LogError("Failed to save conversation to user path");
                return null;
            }

            var adminInbox = new AdminInboxModel
            {
                Id = conversationId,
                UserId = userId,
                UserEmail = userEmail,
                UserDisplayName = userEmail.Split('@')[0],
                Subject = subject,
                Status = "open",
                Priority = priority,
                Category = category,
                LastMessage = "",
                LastMessageAt = now,
                UnreadByAdmin = true,
                AssignedTo = null,
                CreatedAt = now
            };

            await _firestore.AddDocumentAsync(
                "admin_messages",
                adminInbox,
                conversationId
            );

            await UpdateUserUnreadCountAsync(userId);

            await MID_HelperFunctions.DebugMessageAsync(
                $"Conversation created: {conversationId} for user {userId}",
                LogLevel.Info
            );

            return conversationId;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Creating conversation");
            _logger.LogError(ex, "Failed to create conversation for user: {UserId}", userId);
            return null;
        }
    }

    public async Task<List<ConversationModel>> GetUserConversationsAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("GetUserConversations called with empty userId");
                return new List<ConversationModel>();
            }

            var path = $"messages/{userId}/conversations";
            var conversations = await _firestore.GetCollectionAsync<ConversationModel>(path);

            if (conversations == null || !conversations.Any())
                return new List<ConversationModel>();

            var now = DateTime.UtcNow;
            var validConversations = conversations
                .Where(c => !c.ExpiresAt.HasValue || c.ExpiresAt.Value > now)
                .OrderByDescending(c => c.LastMessageAt)
                .ToList();

            return validConversations;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting conversations for user: {userId}");
            _logger.LogError(ex, "Failed to retrieve conversations for user: {UserId}", userId);
            return new List<ConversationModel>();
        }
    }

    public async Task<ConversationModel?> GetConversationAsync(string userId, string conversationId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(conversationId))
            {
                _logger.LogWarning("GetConversation called with invalid parameters");
                return null;
            }

            var path = $"messages/{userId}/conversations";
            return await _firestore.GetDocumentAsync<ConversationModel>(path, conversationId);
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting conversation: {conversationId}");
            _logger.LogError(ex, "Failed to retrieve conversation: {ConversationId}", conversationId);
            return null;
        }
    }

    public async Task<bool> UpdateConversationStatusAsync(string userId, string conversationId, string status)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(conversationId))
            {
                _logger.LogWarning("UpdateConversationStatus called with invalid parameters");
                return false;
            }

            var path = $"messages/{userId}/conversations";
            var updated = await _firestore.UpdateFieldsAsync(
                path,
                conversationId,
                new { status, updated_at = DateTime.UtcNow }
            );

            if (updated)
            {
                await _firestore.UpdateFieldsAsync(
                    "admin_messages",
                    conversationId,
                    new { status }
                );
            }

            return updated;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Updating conversation status");
            _logger.LogError(ex, "Failed to update conversation status: {ConversationId}", conversationId);
            return false;
        }
    }

    public async Task<bool> CloseConversationAsync(string userId, string conversationId)
    {
        return await UpdateConversationStatusAsync(userId, conversationId, "closed");
    }

    // ==================== MESSAGE MANAGEMENT ====================

    public async Task<string?> SendMessageAsync(
        string userId, 
        string conversationId, 
        string senderId, 
        string senderName, 
        string text, 
        List<string>? attachments = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("SendMessage called with invalid parameters");
                return null;
            }

            var messageId = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;

            var message = new MessageModel
            {
                Id = messageId,
                Text = text,
                SenderId = senderId,
                SenderName = senderName,
                Timestamp = now,
                IsRead = senderId == userId,
                Attachments = attachments
            };

            var messagePath = $"messages/{userId}/conversations/{conversationId}/messages";
            var messageSaved = await _firestore.AddToSubcollectionAsync(
                $"messages/{userId}/conversations",
                conversationId,
                "messages",
                message,
                messageId
            );

            if (string.IsNullOrEmpty(messageSaved))
            {
                _logger.LogError("Failed to save message");
                return null;
            }

            var isAdminMessage = senderId == "admin";
            await _firestore.UpdateFieldsAsync(
                $"messages/{userId}/conversations",
                conversationId,
                new 
                { 
                    last_message_at = now,
                    is_admin_replied = isAdminMessage,
                    unread_count = isAdminMessage ? 1 : 0
                }
            );

            await _firestore.UpdateFieldsAsync(
                "admin_messages",
                conversationId,
                new 
                { 
                    last_message = text.Length > 100 ? text.Substring(0, 100) + "..." : text,
                    last_message_at = now,
                    unread_by_admin = !isAdminMessage
                }
            );

            if (isAdminMessage)
            {
                await UpdateUserUnreadCountAsync(userId);
            }

            await MID_HelperFunctions.DebugMessageAsync(
                $"Message sent: {messageId} in conversation {conversationId}",
                LogLevel.Info
            );

            return messageId;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Sending message");
            _logger.LogError(ex, "Failed to send message in conversation: {ConversationId}", conversationId);
            return null;
        }
    }

    public async Task<List<MessageModel>> GetConversationMessagesAsync(string userId, string conversationId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(conversationId))
            {
                _logger.LogWarning("GetConversationMessages called with invalid parameters");
                return new List<MessageModel>();
            }

            var messages = await _firestore.GetSubcollectionAsync<MessageModel>(
                $"messages/{userId}/conversations",
                conversationId,
                "messages"
            );

            return messages ?? new List<MessageModel>();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting messages for conversation: {conversationId}");
            _logger.LogError(ex, "Failed to retrieve messages for conversation: {ConversationId}", conversationId);
            return new List<MessageModel>();
        }
    }

    public async Task<bool> MarkMessagesAsReadAsync(string userId, string conversationId, List<string> messageIds)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(conversationId) || !messageIds.Any())
            {
                _logger.LogWarning("MarkMessagesAsRead called with invalid parameters");
                return false;
            }

            var success = true;
            foreach (var messageId in messageIds)
            {
                var updated = await _firestore.UpdateSubcollectionDocumentAsync(
                    $"messages/{userId}/conversations",
                    conversationId,
                    "messages",
                    messageId,
                    new { is_read = true }
                );

                if (!updated) success = false;
            }

            if (success)
            {
                await _firestore.UpdateFieldsAsync(
                    $"messages/{userId}/conversations",
                    conversationId,
                    new { unread_count = 0 }
                );

                await UpdateUserUnreadCountAsync(userId);
            }

            return success;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Marking messages as read");
            _logger.LogError(ex, "Failed to mark messages as read: {ConversationId}", conversationId);
            return false;
        }
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("GetUnreadCount called with empty userId");
                return 0;
            }

            var profile = await GetUserProfileAsync(userId);
            return profile?.UnreadCount ?? 0;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting unread count for user: {userId}");
            _logger.LogError(ex, "Failed to get unread count for user: {UserId}", userId);
            return 0;
        }
    }

    // ==================== ADMIN OPERATIONS ====================

    public async Task<List<AdminInboxModel>> GetAdminInboxAsync(string? status = null, int limit = 100)
    {
        try
        {
            List<AdminInboxModel> inbox;

            if (string.IsNullOrWhiteSpace(status))
            {
                inbox = await _firestore.GetCollectionAsync<AdminInboxModel>("admin_messages");
            }
            else
            {
                inbox = await _firestore.QueryCollectionAsync<AdminInboxModel>("admin_messages", "status", status);
            }

            if (inbox == null || !inbox.Any())
            {
                return new List<AdminInboxModel>();
            }

            return inbox
                .OrderByDescending(x => x.LastMessageAt)
                .Take(limit)
                .ToList();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Getting admin inbox");
            _logger.LogError(ex, "Failed to retrieve admin inbox");
            return new List<AdminInboxModel>();
        }
    }

    public async Task<bool> AssignConversationAsync(string conversationId, string adminId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(adminId))
            {
                _logger.LogWarning("AssignConversation called with invalid parameters");
                return false;
            }

            return await _firestore.UpdateFieldsAsync(
                "admin_messages",
                conversationId,
                new { assigned_to = adminId }
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Assigning conversation");
            _logger.LogError(ex, "Failed to assign conversation: {ConversationId}", conversationId);
            return false;
        }
    }

    public async Task<List<AdminInboxModel>> GetAdminAssignedConversationsAsync(string adminId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(adminId))
            {
                _logger.LogWarning("GetAdminAssignedConversations called with empty adminId");
                return new List<AdminInboxModel>();
            }

            var assigned = await _firestore.QueryCollectionAsync<AdminInboxModel>(
                "admin_messages", 
                "assigned_to", 
                adminId
            );

            return assigned ?? new List<AdminInboxModel>();
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting assigned conversations for admin: {adminId}");
            _logger.LogError(ex, "Failed to retrieve assigned conversations for admin: {AdminId}", adminId);
            return new List<AdminInboxModel>();
        }
    }

    public async Task<bool> MarkAdminReadAsync(string conversationId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(conversationId))
            {
                _logger.LogWarning("MarkAdminRead called with empty conversationId");
                return false;
            }

            return await _firestore.UpdateFieldsAsync(
                "admin_messages",
                conversationId,
                new { unread_by_admin = false }
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Marking admin read");
            _logger.LogError(ex, "Failed to mark conversation as read by admin: {ConversationId}", conversationId);
            return false;
        }
    }

    // ==================== BULK MESSAGING ====================

    public async Task<Dictionary<string, bool>> SendBulkMessageAsync(
        List<string> userIds, 
        string subject, 
        string message, 
        string category = "system", 
        DateTime? expiresAt = null)
    {
        var results = new Dictionary<string, bool>();

        try
        {
            if (userIds == null || !userIds.Any())
            {
                _logger.LogWarning("SendBulkMessage called with empty user list");
                return results;
            }

            foreach (var userId in userIds)
            {
                try
                {
                    var conversationId = await CreateConversationAsync(
                        userId, 
                        "system@subashaventures.com", 
                        subject, 
                        category
                    );

                    if (!string.IsNullOrEmpty(conversationId))
                    {
                        if (expiresAt.HasValue)
                        {
                            await _firestore.UpdateFieldsAsync(
                                $"messages/{userId}/conversations",
                                conversationId,
                                new { expires_at = expiresAt.Value }
                            );
                        }

                        var messageId = await SendMessageAsync(
                            userId,
                            conversationId,
                            "admin",
                            "System",
                            message
                        );

                        results[userId] = !string.IsNullOrEmpty(messageId);
                    }
                    else
                    {
                        results[userId] = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send bulk message to user: {UserId}", userId);
                    results[userId] = false;
                }
            }

            var successCount = results.Values.Count(x => x);
            await MID_HelperFunctions.DebugMessageAsync(
                $"Bulk message sent: {successCount}/{userIds.Count} successful",
                LogLevel.Info
            );

            return results;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Sending bulk messages");
            _logger.LogError(ex, "Failed to send bulk messages");
            return results;
        }
    }

    public async Task<int> CleanExpiredConversationsAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            var deletedCount = 0;

            _logger.LogInformation("Starting cleanup of expired conversations");

            return deletedCount;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Cleaning expired conversations");
            _logger.LogError(ex, "Failed to clean expired conversations");
            return 0;
        }
    }

    // ==================== USER PROFILE ====================

    public async Task<bool> UpdateUserProfileAsync(string userId, string displayName, string email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("UpdateUserProfile called with empty userId");
                return false;
            }

            var profile = new MessageUserProfileModel
            {
                DisplayName = displayName,
                Email = email,
                UnreadCount = 0,
                LastMessageAt = null
            };

            return await _firestore.AddDocumentAsync(
                $"messages/{userId}",
                profile,
                "profile"
            ) != null;
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, "Updating user profile");
            _logger.LogError(ex, "Failed to update user profile: {UserId}", userId);
            return false;
        }
    }

    public async Task<MessageUserProfileModel?> GetUserProfileAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("GetUserProfile called with empty userId");
                return null;
            }

            return await _firestore.GetDocumentAsync<MessageUserProfileModel>(
                $"messages/{userId}",
                "profile"
            );
        }
        catch (Exception ex)
        {
            await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting user profile: {userId}");
            _logger.LogError(ex, "Failed to retrieve user profile: {UserId}", userId);
            return null;
        }
    }

    // ==================== PRIVATE HELPERS ====================

    private async Task UpdateUserUnreadCountAsync(string userId)
    {
        try
        {
            var conversations = await GetUserConversationsAsync(userId);
            var totalUnread = conversations.Sum(c => c.UnreadCount);

            await _firestore.UpdateFieldsAsync(
                $"messages/{userId}",
                "profile",
                new { unread_count = totalUnread }
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update user unread count: {UserId}", userId);
        }
    }
}
