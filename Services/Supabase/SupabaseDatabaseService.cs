using SubashaVentures.Services.Storage;
using SubashaVentures.Models.Supabase;
using Microsoft.AspNetCore.Components.Authorization;
using Newtonsoft.Json;
using Supabase.Postgrest.Models;
using static Supabase.Postgrest.Constants;
using Client = Supabase.Client;
using Realtime = Supabase.Realtime;

namespace SubashaVentures.Services.SupaBase
{
    public class SupabaseDatabaseService : ISupabaseDatabaseService
    {
        private readonly Client _client;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly IBlazorAppLocalStorageService _localStorage;
        private readonly ILogger<SupabaseDatabaseService> _logger;
        private bool _initialized = false;

        public SupabaseDatabaseService(
            Client client,
            // REMOVED: AuthenticationStateProvider authStateProvider,
            IBlazorAppLocalStorageService localStorage,
            ILogger<SupabaseDatabaseService> logger)
        {
            _logger = logger;
            _logger.LogInformation("------------------- DATABASE SERVICE CONSTRUCTOR -------------------");
            _client = client;
            // REMOVED: _authStateProvider = authStateProvider;
            _localStorage = localStorage;
        }

        public async Task InitializeAsync()
        {
            if (!_initialized)
            {
                try
                {
                    await _client.InitializeAsync();
                    _initialized = true;
                    _logger.LogInformation("Supabase client initialized successfully");
                }
                catch (Realtime.Exceptions.RealtimeException ex) when (ex.InnerException is System.PlatformNotSupportedException)
                {
                    _logger.LogWarning("Realtime features disabled due to WebAssembly platform limitations: {Message}", ex.Message);
                    _initialized = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize Supabase client");
                    throw;
                }
            }
        }

        #region Core CRUD Operations
        public async Task<IReadOnlyList<TModel>> GetAllAsync<TModel>() where TModel : BaseModel, new()
        {
            await EnsureInitializedAsync();
            
            try
            {
                var response = await _client.From<TModel>().Get();
                return response.Models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all items of type {ModelType}", typeof(TModel).Name);
                return new List<TModel>();
            }
        }

        public async Task<TModel?> GetByIdAsync<TModel>(int id) where TModel : BaseModel, new()
        {
            await EnsureInitializedAsync();
            
            try
            {
                return await _client
                    .From<TModel>()
                    .Filter("id", Operator.Equals, id)
                    .Single();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving {ModelType} by ID: {Id}", typeof(TModel).Name, id);
                return null;
            }
        }

        public async Task<List<TModel>> InsertAsync<TModel>(TModel item) where TModel : BaseModel, new()
        {
            await EnsureInitializedAsync();
    
            try
            {
                // 🔍 DEBUG: Log the item before insertion
                var itemType = typeof(TModel).Name;
                _logger.LogInformation("=== INSERT DEBUG START ===");
                _logger.LogInformation("Inserting {ModelType}", itemType);
        
                // Check if item has Id property
                var idProperty = typeof(TModel).GetProperty("Id");
                if (idProperty != null)
                {
                    var idValue = idProperty.GetValue(item);
                    _logger.LogInformation("ID Property Value: {IdValue} (Type: {IdType})", 
                        idValue ?? "NULL", 
                        idValue?.GetType().Name ?? "NULL");
                }
        
                // Serialize to see what's being sent
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(item, 
                    new Newtonsoft.Json.JsonSerializerSettings 
                    { 
                        NullValueHandling = Newtonsoft.Json.NullValueHandling.Include,
                        Formatting = Newtonsoft.Json.Formatting.Indented
                    });
                _logger.LogInformation("Serialized JSON:\n{Json}", json);
                _logger.LogInformation("=== INSERT DEBUG END ===");
        
                var response = await _client.From<TModel>().Insert(item);
                _logger.LogInformation("Successfully inserted {ModelType}", itemType);
        
                return response.Models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting {ModelType}", typeof(TModel).Name);
                throw;
            }
        }

        public async Task<List<TModel>> UpdateAsync<TModel>(TModel item) where TModel : BaseModel, new()
        {
            await EnsureInitializedAsync();
            
            try
            {
                var response = await _client.From<TModel>().Update(item);
                _logger.LogInformation("Successfully updated {ModelType}", typeof(TModel).Name);
                return response.Models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating {ModelType}", typeof(TModel).Name);
                throw;
            }
        }

        public async Task<List<TModel>> DeleteAsync<TModel>(TModel item) where TModel : BaseModel, new()
        {
            await EnsureInitializedAsync();
            
            try
            {
                var response = await _client.From<TModel>().Delete(item);
                _logger.LogInformation("Successfully deleted {ModelType}", typeof(TModel).Name);
                return response.Models;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting {ModelType}", typeof(TModel).Name);
                throw;
            }
        }

        public async Task<List<TModel>> SoftDeleteAsync<TModel>(TModel item) where TModel : BaseModel, new()
        {
            await EnsureInitializedAsync();
            
            try
            {
                // Implementation depends on model having soft delete properties
                return new List<TModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error soft deleting {ModelType}", typeof(TModel).Name);
                throw;
            }
        }

        public async Task<List<TModel>> GetWithFilterAsync<TModel>(string columnName, Operator filterOperator, object value) 
            where TModel : BaseModel, new()
        {
            await EnsureInitializedAsync();
    
            try
            {
                var result = await _client
                    .From<TModel>()
                    .Filter(columnName, filterOperator, value)
                    .Get();
        
                return result?.Models ?? new List<TModel>();
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON deserialization error for {ModelType}. Column: {Column}, Value: {Value}", 
                    typeof(TModel).Name, columnName, value);
                return new List<TModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database query error for {ModelType}. Column: {Column}, Value: {Value}", 
                    typeof(TModel).Name, columnName, value);
                return new List<TModel>();
            }
        }

        public async Task<T> ExecuteRpcAsync<T>(string functionName, object? parameters)
        {
            await EnsureInitializedAsync();
            
            try
            {
                return await _client.Rpc<T>(functionName, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing RPC function: {FunctionName}", functionName);
                throw;
            }
        }
        #endregion

        #region Security Violation Operations
        public async Task<bool> LogSecurityViolationAsync(string userId, string userEmail, 
            List<SecurityViolation> violations, Dictionary<string, object>? metadata = null)
        {
            try
            {
                var log = new SecurityViolationLog
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    TimeSent = DateTime.UtcNow,
                    ExpiryAt = DateTime.UtcNow.AddDays(30)
                };

                log.SetViolations(violations);
                log.SetMetadata(metadata ?? new Dictionary<string, object>());

                await InsertAsync(log);

                // Backup critical violations locally
                if (IsCriticalViolation(violations))
                    await BackupCriticalDataLocallyAsync(log, "security_violations");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log security violation for user {UserId}", userId);
                return false;
            }
        }

        public async Task<List<SecurityViolationLog>> GetSecurityViolationsAsync(string? userId = null)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    return (await GetAllAsync<SecurityViolationLog>()).ToList();
                
                return await GetWithFilterAsync<SecurityViolationLog>("user_id", Operator.Equals, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve security violations");
                return new List<SecurityViolationLog>();
            }
        }

        public async Task<bool> CleanExpiredSecurityViolationsAsync()
        {
            try
            {
                var expired = await GetWithFilterAsync<SecurityViolationLog>("expiry_at", Operator.LessThan, DateTime.UtcNow);
                
                foreach (var item in expired)
                    await DeleteAsync(item);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clean expired security violations");
                return false;
            }
        }
        #endregion

        #region Admin Message Operations
        public async Task<bool> SendMessageToAdminAsync(string userId, string userEmail,
            List<AdminMessage> messages, Dictionary<string, object>? metadata = null)
        {
            try
            {
                var message = new MessageToSuperiorAdmin
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    TimeSent = DateTime.UtcNow,
                    ExpiryAt = DateTime.UtcNow.AddDays(30)
                };

                message.SetMessages(messages);
                message.SetMetadata(metadata ?? new Dictionary<string, object>());

                await InsertAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to admin for user {UserId}", userId);
                return false;
            }
        }

        public async Task<List<MessageToSuperiorAdmin>> GetAdminMessagesAsync(string? userId = null)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                    return (await GetAllAsync<MessageToSuperiorAdmin>()).ToList();
                
                return await GetWithFilterAsync<MessageToSuperiorAdmin>("user_id", Operator.Equals, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve admin messages");
                return new List<MessageToSuperiorAdmin>();
            }
        }

        public async Task<bool> CleanExpiredAdminMessagesAsync()
        {
            try
            {
                var expired = await GetWithFilterAsync<MessageToSuperiorAdmin>("expiry_at", Operator.LessThan, DateTime.UtcNow);
                
                foreach (var item in expired)
                    await DeleteAsync(item);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clean expired admin messages");
                return false;
            }
        }
        #endregion

        #region User Notification Operations
        public async Task<bool> SendNotificationAsync(string senderUserId, string senderEmail,
            string receiverUserId, string receiverEmail, List<NotificationMessage> messages,
            bool isSystemMessage = false, Dictionary<string, object>? metadata = null)
        {
            try
            {
                var notification = new UserNotificationMessage
                {
                    SenderUserId = senderUserId,
                    SenderEmail = senderEmail,
                    ReceiverUserId = receiverUserId,
                    ReceiverEmail = receiverEmail,
                    IsSystemMessage = isSystemMessage,
                    TimeSent = DateTime.UtcNow,
                    ExpiryAt = DateTime.UtcNow.AddDays(7)
                };

                notification.SetMessages(messages);
                notification.SetMetadata(metadata ?? new Dictionary<string, object>());

                await InsertAsync(notification);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification from {SenderId} to {ReceiverId}", 
                    senderUserId, receiverUserId);
                return false;
            }
        }

        public async Task<List<UserNotificationMessage>> GetUserNotificationsAsync(string userId, bool unreadOnly = false)
        {
            try
            {
                var notifications = await GetWithFilterAsync<UserNotificationMessage>("receiver_user_id", Operator.Equals, userId);
                
                if (unreadOnly)
                    return notifications.Where(n => !n.IsRead).ToList();
                
                return notifications;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve notifications for user {UserId}", userId);
                return new List<UserNotificationMessage>();
            }
        }

        public async Task<bool> MarkNotificationAsReadAsync(long notificationId)
        {
            try
            {
                var notification = await GetByIdAsync<UserNotificationMessage>((int)notificationId);
                if (notification == null) return false;

                notification.IsRead = true;
                await UpdateAsync(notification);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark notification {Id} as read", notificationId);
                return false;
            }
        }

        public async Task<bool> CleanExpiredNotificationsAsync()
        {
            try
            {
                var expired = await GetWithFilterAsync<UserNotificationMessage>("expiry_at", Operator.LessThan, DateTime.UtcNow);
                
                foreach (var item in expired)
                    await DeleteAsync(item);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clean expired notifications");
                return false;
            }
        }
        #endregion

        #region Local Backup Operations
        public async Task BackupCriticalDataLocallyAsync(object data, string dataType)
        {
            try
            {
                var backupKey = $"backup_{dataType}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
                await _localStorage.SetItemAsync(backupKey, data);
                
                // Keep only last 10 backups per type
                await CleanOldBackups(dataType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to backup {DataType} locally", dataType);
            }
        }

        public async Task<List<T>> GetLocalBackupsAsync<T>(string dataType)
        {
            var backups = new List<T>();
            
            try
            {
                // This is a simplified approach - you'd need to implement key enumeration
                // based on your specific local storage implementation
                for (int i = 0; i < 10; i++)
                {
                    var key = $"backup_{dataType}_{i}";
                    if (await _localStorage.ContainsKeyAsync(key))
                    {
                        var backup = await _localStorage.GetItemAsync<T>(key);
                        if (backup != null) backups.Add(backup);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve local backups for {DataType}", dataType);
            }
            
            return backups;
        }

        private async Task CleanOldBackups(string dataType)
        {
            // Implementation would enumerate and remove old backups
            // keeping only the most recent 10 for the specified data type
        }
        #endregion

        #region Private Helper Methods
        private async Task EnsureInitializedAsync()
        {
            if (!_initialized)
                await InitializeAsync();
        }
        

        private bool IsCriticalViolation(List<SecurityViolation> violations)
        {
          
            return violations.Any(v => v.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase) ||
                                     v.Severity.Equals("high", StringComparison.OrdinalIgnoreCase));
        }
        #endregion
    }
}