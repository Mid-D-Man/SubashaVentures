using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace SubashaVentures.Models.Supabase;
using System;
using System.Collections.Generic;
using System.Text.Json;

    [Table("SECURITY_VIOLATION_LOGS")]
    public class SecurityViolationLog : BaseModel
    {
        [PrimaryKey("user_id")]
        public string UserId { get; set; } = string.Empty;
        
        [Column("user_email")]
        public string UserEmail { get; set; } = string.Empty;
        
        [Column("metadata")]
        public string Metadata { get; set; } = "{}";
        
        [Column("violations")]
        public string Violations { get; set; } = "[]";
        
        [Column("time_sent")]
        public DateTime TimeSent { get; set; }
        
        [Column("expiry_at")]
        public DateTime ExpiryAt { get; set; }

        public Dictionary<string, object> GetMetadata()
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(Metadata) 
                       ?? new Dictionary<string, object>();
            }
            catch { return new Dictionary<string, object>(); }
        }

        public void SetMetadata(Dictionary<string, object> metadata)
        {
            Metadata = JsonSerializer.Serialize(metadata);
        }

        public List<SecurityViolation> GetViolations()
        {
            try
            {
                return JsonSerializer.Deserialize<List<SecurityViolation>>(Violations) 
                       ?? new List<SecurityViolation>();
            }
            catch { return new List<SecurityViolation>(); }
        }

        public void SetViolations(List<SecurityViolation> violations)
        {
            Violations = JsonSerializer.Serialize(violations);
        }
    }

    [Table("MESSAGES_TO_SUPERIOR_ADMIN")]
    public class MessageToSuperiorAdmin : BaseModel
    {
        [PrimaryKey("user_id")]
        public string UserId { get; set; } = string.Empty;
        
        [Column("user_email")]
        public string UserEmail { get; set; } = string.Empty;
        
        [Column("metadata")]
        public string Metadata { get; set; } = "{}";
        
        [Column("messages")]
        public string Messages { get; set; } = "[]";
        
        [Column("time_sent")]
        public DateTime TimeSent { get; set; }
        
        [Column("expiry_at")]
        public DateTime ExpiryAt { get; set; }

        public Dictionary<string, object> GetMetadata()
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(Metadata) 
                       ?? new Dictionary<string, object>();
            }
            catch { return new Dictionary<string, object>(); }
        }

        public void SetMetadata(Dictionary<string, object> metadata)
        {
            Metadata = JsonSerializer.Serialize(metadata);
        }

        public List<AdminMessage> GetMessages()
        {
            try
            {
                return JsonSerializer.Deserialize<List<AdminMessage>>(Messages) 
                       ?? new List<AdminMessage>();
            }
            catch { return new List<AdminMessage>(); }
        }

        public void SetMessages(List<AdminMessage> messages)
        {
            Messages = JsonSerializer.Serialize(messages);
        }
    }

    [Table("USER_NOTIFICATIONS_MESSAGES")]
    public class UserNotificationMessage : BaseModel
    {
        [PrimaryKey("id")]
        public long Id { get; set; }
        
        [Column("sender_user_id")]
        public string SenderUserId { get; set; } = string.Empty;
        
        [Column("sender_email")]
        public string SenderEmail { get; set; } = string.Empty;
        
        [Column("receiver_user_id")]
        public string ReceiverUserId { get; set; } = string.Empty;
        
        [Column("receiver_email")]
        public string ReceiverEmail { get; set; } = string.Empty;
        
        [Column("metadata")]
        public string Metadata { get; set; } = "{}";
        
        [Column("messages")]
        public string Messages { get; set; } = "[]";
        
        [Column("is_system_message")]
        public bool IsSystemMessage { get; set; } = false;
        
        [Column("is_read")]
        public bool IsRead { get; set; } = false;
        
        [Column("time_sent")]
        public DateTime TimeSent { get; set; }
        
        [Column("expiry_at")]
        public DateTime ExpiryAt { get; set; }

        public Dictionary<string, object> GetMetadata()
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(Metadata) 
                       ?? new Dictionary<string, object>();
            }
            catch { return new Dictionary<string, object>(); }
        }

        public void SetMetadata(Dictionary<string, object> metadata)
        {
            Metadata = JsonSerializer.Serialize(metadata);
        }

        public List<NotificationMessage> GetMessages()
        {
            try
            {
                return JsonSerializer.Deserialize<List<NotificationMessage>>(Messages) 
                       ?? new List<NotificationMessage>();
            }
            catch { return new List<NotificationMessage>(); }
        }

        public void SetMessages(List<NotificationMessage> messages)
        {
            Messages = JsonSerializer.Serialize(messages);
        }
    }

    public class SecurityViolation
    {
        public string ViolationHeader { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Severity { get; set; } = "low";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class AdminMessage
    {
        public string Header { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Priority { get; set; } = "normal";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class NotificationMessage
    {
        public string Header { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Type { get; set; } = "info";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
