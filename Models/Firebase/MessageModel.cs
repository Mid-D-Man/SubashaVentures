// Models/Firebase/MessageModel.cs - NEW
// Firebase message structure for user support/communications
namespace SubashaVentures.Models.Firebase;

/// <summary>
/// User support conversation stored in Firebase
/// Path: messages/{userId}/conversations/{conversationId}
/// </summary>
public class ConversationModel
{
    public string Id { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = "open"; // open, resolved, closed
    public string Priority { get; set; } = "normal"; // low, normal, high, urgent
    public string Category { get; set; } = "question"; // complaint, question, feedback, order_issue
    public DateTime CreatedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public bool IsAdminReplied { get; set; }
    public int UnreadCount { get; set; }
}

/// <summary>
/// Individual message in a conversation
/// Path: messages/{userId}/conversations/{conversationId}/messages/{messageId}
/// </summary>
public class MessageModel
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty; // user ID or 'admin'
    public string SenderName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsRead { get; set; }
    public List<string>? Attachments { get; set; } // Optional file URLs
}

/// <summary>
/// User profile snapshot in Firebase messages
/// Path: messages/{userId}/profile
/// </summary>
public class MessageUserProfileModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int UnreadCount { get; set; }
    public DateTime? LastMessageAt { get; set; }
}

/// <summary>
/// Admin inbox entry (mirrored for quick access)
/// Path: admin_messages/{conversationId}
/// </summary>
public class AdminInboxModel
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = "open";
    public string Priority { get; set; } = "normal";
    public string Category { get; set; } = string.Empty;
    public string LastMessage { get; set; } = string.Empty;
    public DateTime? LastMessageAt { get; set; }
    public bool UnreadByAdmin { get; set; }
    public string? AssignedTo { get; set; } // Admin ID
    public DateTime CreatedAt { get; set; }
}
