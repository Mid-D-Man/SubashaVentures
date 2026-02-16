// Models/Firebase/MessageModel.cs - MESSAGE SYSTEM MODELS
using System.Text.Json.Serialization;

namespace SubashaVentures.Models.Firebase;

/// <summary>
/// Individual message within a conversation
/// Stored in messages/{userId}/conversations/{conversationId}/messages/{messageId}
/// </summary>
public class MessageModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
    
    [JsonPropertyName("sender_id")]
    public string SenderId { get; set; } = string.Empty;
    
    [JsonPropertyName("sender_name")]
    public string SenderName { get; set; } = string.Empty;
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
    
    [JsonPropertyName("is_read")]
    public bool IsRead { get; set; }
    
    [JsonPropertyName("attachments")]
    public List<string>? Attachments { get; set; }
}

/// <summary>
/// Conversation metadata
/// Stored in messages/{userId}/conversations/{conversationId}
/// </summary>
public class ConversationModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;
    
    [JsonPropertyName("category")]
    public string Category { get; set; } = "system"; // system, promotion, support
    
    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "normal"; // normal, high
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = "open"; // open, closed, archived
    
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [JsonPropertyName("last_message_at")]
    public DateTime LastMessageAt { get; set; }
    
    [JsonPropertyName("expires_at")]
    public DateTime? ExpiresAt { get; set; }
    
    [JsonPropertyName("is_admin_replied")]
    public bool IsAdminReplied { get; set; }
    
    [JsonPropertyName("unread_count")]
    public int UnreadCount { get; set; }
}

/// <summary>
/// User messaging profile
/// Stored in messages/{userId}/profile
/// </summary>
public class MessageUserProfileModel
{
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;
    
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
    
    [JsonPropertyName("unread_count")]
    public int UnreadCount { get; set; }
    
    [JsonPropertyName("last_message_at")]
    public DateTime? LastMessageAt { get; set; }
}

/// <summary>
/// Admin inbox entry - mirrors conversations for admin view
/// Stored in admin_messages/{conversationId}
/// </summary>
public class AdminInboxModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;
    
    [JsonPropertyName("user_email")]
    public string UserEmail { get; set; } = string.Empty;
    
    [JsonPropertyName("user_display_name")]
    public string UserDisplayName { get; set; } = string.Empty;
    
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;
    
    [JsonPropertyName("category")]
    public string Category { get; set; } = "system";
    
    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "normal";
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = "open";
    
    [JsonPropertyName("last_message")]
    public string LastMessage { get; set; } = string.Empty;
    
    [JsonPropertyName("last_message_at")]
    public DateTime LastMessageAt { get; set; }
    
    [JsonPropertyName("unread_by_admin")]
    public bool UnreadByAdmin { get; set; }
    
    [JsonPropertyName("assigned_to")]
    public string? AssignedTo { get; set; }
    
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Promotion message model - targeted promotions based on user behavior
/// </summary>
public class PromotionMessageModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;
    
    [JsonPropertyName("promotion_type")]
    public string PromotionType { get; set; } = string.Empty; // discount, featured, recommendation
    
    [JsonPropertyName("product_ids")]
    public List<string> ProductIds { get; set; } = new();
    
    [JsonPropertyName("discount_percentage")]
    public int? DiscountPercentage { get; set; }
    
    [JsonPropertyName("valid_until")]
    public DateTime? ValidUntil { get; set; }
    
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}
