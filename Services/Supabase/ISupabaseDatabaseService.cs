
using Supabase.Postgrest;
using Supabase.Postgrest.Models;
using SubashaVentures.Models.Supabase;

namespace SubashaVentures.Services.SupaBase
{
    public interface ISupabaseDatabaseService
    {
        /// <summary>
        /// Initialize the Supabase client connection
        /// </summary>
        Task InitializeAsync();
        
        /// <summary>
        /// Get all items from a table
        /// </summary>
        Task<IReadOnlyList<TModel>> GetAllAsync<TModel>() where TModel : BaseModel, new();
        
        /// <summary>
        /// Get a single item from a table by its ID
        /// </summary>
        Task<TModel?> GetByIdAsync<TModel>(int id) where TModel : BaseModel, new();
        
        /// <summary>
        /// Insert a new item into a table
        /// </summary>
        Task<List<TModel>> InsertAsync<TModel>(TModel item) where TModel : BaseModel, new();
        
        /// <summary>
        /// Update an existing item in a table
        /// </summary>
        Task<List<TModel>> UpdateAsync<TModel>(TModel item) where TModel : BaseModel, new();
        
        /// <summary>
        /// Delete an item from a table
        /// </summary>
        Task<List<TModel>> DeleteAsync<TModel>(TModel item) where TModel : BaseModel, new();
        
        /// <summary>
        /// Soft delete implementation for items with SoftDelete property
        /// </summary>
        Task<List<TModel>> SoftDeleteAsync<TModel>(TModel item) where TModel : BaseModel, new();
        
        /// <summary>
        /// Get items from a table with filtering
        /// </summary>
        Task<List<TModel>> GetWithFilterAsync<TModel>(string columnName, Constants.Operator filterOperator, object value) where TModel : BaseModel, new();
        
        /// <summary>
        /// Execute a custom RPC function
        /// </summary>
        Task<T> ExecuteRpcAsync<T>(string functionName, object? parameters);

        // Security Violation Logs
        Task<bool> LogSecurityViolationAsync(string userId, string userEmail, List<SecurityViolation> violations, Dictionary<string, object>? metadata = null);
        Task<List<SecurityViolationLog>> GetSecurityViolationsAsync(string? userId = null);
        Task<bool> CleanExpiredSecurityViolationsAsync();

        // Messages to Superior Admin
        Task<bool> SendMessageToAdminAsync(string userId, string userEmail, List<AdminMessage> messages, Dictionary<string, object>? metadata = null);
        Task<List<MessageToSuperiorAdmin>> GetAdminMessagesAsync(string? userId = null);
        Task<bool> CleanExpiredAdminMessagesAsync();

        // User Notification Messages
        Task<bool> SendNotificationAsync(string senderUserId, string senderEmail, string receiverUserId, string receiverEmail, List<NotificationMessage> messages, bool isSystemMessage = false, Dictionary<string, object>? metadata = null);
        Task<List<UserNotificationMessage>> GetUserNotificationsAsync(string userId, bool unreadOnly = false);
        Task<bool> MarkNotificationAsReadAsync(long notificationId);
        Task<bool> CleanExpiredNotificationsAsync();
        
        // Local Storage Backup for Critical Messages
        Task BackupCriticalDataLocallyAsync(object data, string dataType);
        Task<List<T>> GetLocalBackupsAsync<T>(string dataType);
    }
}