// Services/Firebase/IMessagingService.cs - NEW
using SubashaVentures.Models.Firebase;

namespace SubashaVentures.Services.Firebase;

/// <summary>
/// Service for Firebase-based user messaging and support system
/// </summary>
public interface IMessagingService
{
    // ==================== CONVERSATION MANAGEMENT ====================
    
    /// <summary>
    /// Create a new support conversation
    /// </summary>
    Task<string?> CreateConversationAsync(string userId, string userEmail, string subject, string category, string priority = "normal");
    
    /// <summary>
    /// Get all conversations for a user
    /// </summary>
    Task<List<ConversationModel>> GetUserConversationsAsync(string userId);
    
    /// <summary>
    /// Get a specific conversation
    /// </summary>
    Task<ConversationModel?> GetConversationAsync(string userId, string conversationId);
    
    /// <summary>
    /// Update conversation status
    /// </summary>
    Task<bool> UpdateConversationStatusAsync(string userId, string conversationId, string status);
    
    /// <summary>
    /// Close a conversation
    /// </summary>
    Task<bool> CloseConversationAsync(string userId, string conversationId);
    
    // ==================== MESSAGE MANAGEMENT ====================
    
    /// <summary>
    /// Send a message in a conversation
    /// </summary>
    Task<string?> SendMessageAsync(string userId, string conversationId, string senderId, string senderName, string text, List<string>? attachments = null);
    
    /// <summary>
    /// Get all messages in a conversation
    /// </summary>
    Task<List<MessageModel>> GetConversationMessagesAsync(string userId, string conversationId);
    
    /// <summary>
    /// Mark messages as read
    /// </summary>
    Task<bool> MarkMessagesAsReadAsync(string userId, string conversationId, List<string> messageIds);
    
    /// <summary>
    /// Get unread message count for user
    /// </summary>
    Task<int> GetUnreadCountAsync(string userId);
    
    // ==================== ADMIN OPERATIONS ====================
    
    /// <summary>
    /// Get all conversations in admin inbox
    /// </summary>
    Task<List<AdminInboxModel>> GetAdminInboxAsync(string? status = null, int limit = 100);
    
    /// <summary>
    /// Assign conversation to admin
    /// </summary>
    Task<bool> AssignConversationAsync(string conversationId, string adminId);
    
    /// <summary>
    /// Get admin's assigned conversations
    /// </summary>
    Task<List<AdminInboxModel>> GetAdminAssignedConversationsAsync(string adminId);
    
    /// <summary>
    /// Mark conversation as read by admin
    /// </summary>
    Task<bool> MarkAdminReadAsync(string conversationId);
    
    // ==================== USER PROFILE ====================
    
    /// <summary>
    /// Update user messaging profile
    /// </summary>
    Task<bool> UpdateUserProfileAsync(string userId, string displayName, string email);
    
    /// <summary>
    /// Get user messaging profile
    /// </summary>
    Task<MessageUserProfileModel?> GetUserProfileAsync(string userId);
}
