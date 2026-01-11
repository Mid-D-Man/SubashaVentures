// Services/Firebase/FirestoreService.cs - FIXED: Better error handling and initialization

using SubashaVentures.Services.Storage;
using Microsoft.JSInterop;
using SubashaVentures.Utilities.HelperScripts;
using System.Text.Json;
using LogLevel = SubashaVentures.Utilities.Logging.LogLevel;

namespace SubashaVentures.Services.Firebase
{
    public class FirestoreService : IFirestoreService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly IBlazorAppLocalStorageService _localStorage;
        private bool _isInitialized = false;
        private bool _isInitializing = false;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);

        public bool IsInitialized => _isInitialized;
        public bool IsConfigured { get; private set; }

        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        public FirestoreService(IJSRuntime jsRuntime, IBlazorAppLocalStorageService localStorage)
        {
            _jsRuntime = jsRuntime;
            _localStorage = localStorage ?? throw new NullReferenceException("Missing localStorage service reference");
        }

        private async Task<bool> EnsureInitializedAsync()
        {
            // Fast path: already initialized
            if (_isInitialized)
            {
                return true;
            }

            // Prevent multiple simultaneous initialization attempts
            await _initLock.WaitAsync();
            
            try
            {
                // Double-check after acquiring lock
                if (_isInitialized)
                {
                    return true;
                }

                // Don't initialize if already initializing
                if (_isInitializing)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "Firestore initialization already in progress...", 
                        LogLevel.Info
                    );
                    
                    // Wait a bit and check again
                    await Task.Delay(500);
                    return _isInitialized;
                }

                _isInitializing = true;

                await MID_HelperFunctions.DebugMessageAsync(
                    "🔄 Ensuring Firestore is initialized...", 
                    LogLevel.Info
                );

                // Call JavaScript initialization
                _isInitialized = await _jsRuntime.InvokeAsync<bool>("firestoreModule.initializeFirestore");
                IsConfigured = _isInitialized;
                
                if (_isInitialized)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "✅ Firestore is ready", 
                        LogLevel.Info
                    );
                }
                else
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "❌ Firestore initialization returned false", 
                        LogLevel.Error
                    );
                }

                return _isInitialized;
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, "Ensuring Firestore initialization");
                _isInitialized = false;
                IsConfigured = false;
                return false;
            }
            finally
            {
                _isInitializing = false;
                _initLock.Release();
            }
        }

        // ==================== DOCUMENT OPERATIONS ====================

        public async Task<T> GetDocumentAsync<T>(string collection, string id) where T : class
        {
            try
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"📖 C# GetDocumentAsync: {collection}/{id}",
                    LogLevel.Debug
                );

                // Ensure Firestore is initialized
                var initialized = await EnsureInitializedAsync();
                if (!initialized)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "❌ Cannot get document - Firestore not initialized",
                        LogLevel.Error
                    );
                    return null;
                }

                var jsonResult = await _jsRuntime.InvokeAsync<string>(
                    "firestoreModule.getDocument", 
                    collection, 
                    id
                );
                
                if (string.IsNullOrEmpty(jsonResult))
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"⚠️ Document not found: {collection}/{id}",
                        LogLevel.Warning
                    );
                    return null;
                }
                
                var result = JsonSerializer.Deserialize<T>(jsonResult, _jsonOptions);
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ Document retrieved: {collection}/{id}",
                    LogLevel.Debug
                );
                
                return result;
            }
            catch (JSException jsEx)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"❌ JavaScript error getting document {collection}/{id}: {jsEx.Message}",
                    LogLevel.Error
                );
                return null;
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting document: {collection}/{id}");
                return null;
            }
        }

        public async Task<string> AddDocumentAsync<T>(string collection, T data, string customId = null) where T : class
        {
            try
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"➕ C# AddDocumentAsync: {collection}{(customId != null ? $"/{customId}" : "")}",
                    LogLevel.Debug
                );

                var initialized = await EnsureInitializedAsync();
                if (!initialized)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "❌ Cannot add document - Firestore not initialized",
                        LogLevel.Error
                    );
                    return null;
                }

                var json = JsonSerializer.Serialize(data, _jsonOptions);
                var docId = await _jsRuntime.InvokeAsync<string>(
                    "firestoreModule.addDocument", 
                    collection, 
                    json, 
                    customId
                );

                if (!string.IsNullOrEmpty(docId))
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✅ Document added: {collection}/{docId}",
                        LogLevel.Info
                    );
                }

                return docId;
            }
            catch (JSException jsEx)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"❌ JavaScript error adding document to {collection}: {jsEx.Message}",
                    LogLevel.Error
                );
                return null;
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, $"Adding document to: {collection}");
                return null;
            }
        }

        public async Task<bool> UpdateDocumentAsync<T>(string collection, string id, T data) where T : class
        {
            try
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✏️ C# UpdateDocumentAsync: {collection}/{id}",
                    LogLevel.Debug
                );

                var initialized = await EnsureInitializedAsync();
                if (!initialized)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "❌ Cannot update document - Firestore not initialized",
                        LogLevel.Error
                    );
                    return false;
                }

                var json = JsonSerializer.Serialize(data, _jsonOptions);
                var success = await _jsRuntime.InvokeAsync<bool>(
                    "firestoreModule.updateDocument", 
                    collection, 
                    id, 
                    json
                );

                if (success)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✅ Document updated: {collection}/{id}",
                        LogLevel.Info
                    );
                }

                return success;
            }
            catch (JSException jsEx)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"❌ JavaScript error updating document {collection}/{id}: {jsEx.Message}",
                    LogLevel.Error
                );
                return false;
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, $"Updating document: {collection}/{id}");
                return false;
            }
        }

        public async Task<bool> DeleteDocumentAsync(string collection, string id)
        {
            try
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"🗑️ C# DeleteDocumentAsync: {collection}/{id}",
                    LogLevel.Debug
                );

                var initialized = await EnsureInitializedAsync();
                if (!initialized)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "❌ Cannot delete document - Firestore not initialized",
                        LogLevel.Error
                    );
                    return false;
                }

                var success = await _jsRuntime.InvokeAsync<bool>(
                    "firestoreModule.deleteDocument", 
                    collection, 
                    id
                );

                if (success)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"✅ Document deleted: {collection}/{id}",
                        LogLevel.Info
                    );
                }

                return success;
            }
            catch (JSException jsEx)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"❌ JavaScript error deleting document {collection}/{id}: {jsEx.Message}",
                    LogLevel.Error
                );
                return false;
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, $"Deleting document: {collection}/{id}");
                return false;
            }
        }

        // ==================== FIELD OPERATIONS ====================

        public async Task<bool> AddOrUpdateFieldAsync<T>(string collection, string docId, string fieldName, T value)
        {
            try
            {
                var initialized = await EnsureInitializedAsync();
                if (!initialized) return false;

                var json = JsonSerializer.Serialize(value, _jsonOptions);
                return await _jsRuntime.InvokeAsync<bool>(
                    "firestoreModule.addOrUpdateField", 
                    collection, 
                    docId, 
                    fieldName, 
                    json
                );
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, $"Updating field: {fieldName}");
                return false;
            }
        }

        public async Task<bool> UpdateFieldsAsync<T>(string collection, string docId, T fields) where T : class
        {
            try
            {
                var initialized = await EnsureInitializedAsync();
                if (!initialized) return false;

                var json = JsonSerializer.Serialize(fields, _jsonOptions);
                return await _jsRuntime.InvokeAsync<bool>(
                    "firestoreModule.updateFields", 
                    collection, 
                    docId, 
                    json
                );
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, "Updating fields");
                return false;
            }
        }

        public async Task<bool> RemoveFieldAsync(string collection, string docId, string fieldName)
        {
            try
            {
                var initialized = await EnsureInitializedAsync();
                if (!initialized) return false;

                return await _jsRuntime.InvokeAsync<bool>(
                    "firestoreModule.removeField", 
                    collection, 
                    docId, 
                    fieldName
                );
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, $"Removing field: {fieldName}");
                return false;
            }
        }

        public async Task<bool> RemoveFieldsAsync(string collection, string docId, List<string> fieldNames)
        {
            try
            {
                var initialized = await EnsureInitializedAsync();
                if (!initialized) return false;

                var json = JsonSerializer.Serialize(fieldNames, _jsonOptions);
                return await _jsRuntime.InvokeAsync<bool>(
                    "firestoreModule.removeFields", 
                    collection, 
                    docId, 
                    json
                );
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, "Removing fields");
                return false;
            }
        }

        public async Task<T> GetFieldAsync<T>(string collection, string docId, string fieldName)
        {
            try
            {
                var initialized = await EnsureInitializedAsync();
                if (!initialized) return default(T);

                var jsonResult = await _jsRuntime.InvokeAsync<string>(
                    "firestoreModule.getField", 
                    collection, 
                    docId, 
                    fieldName
                );
                
                if (string.IsNullOrEmpty(jsonResult))
                    return default(T);
                
                return JsonSerializer.Deserialize<T>(jsonResult, _jsonOptions);
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting field: {fieldName}");
                return default(T);
            }
        }

        // ==================== ARRAY FIELD OPERATIONS ====================

        public async Task<bool> AddToArrayFieldAsync<T>(string collection, string docId, string fieldName, T value)
        {
            try
            {
                var initialized = await EnsureInitializedAsync();
                if (!initialized) return false;

                var json = JsonSerializer.Serialize(value, _jsonOptions);
                return await _jsRuntime.InvokeAsync<bool>(
                    "firestoreModule.addToArrayField", 
                    collection, 
                    docId, 
                    fieldName, 
                    json
                );
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, $"Adding to array field: {fieldName}");
                return false;
            }
        }

        public async Task<bool> RemoveFromArrayFieldAsync<T>(string collection, string docId, string fieldName, T value)
        {
            try
            {
                var initialized = await EnsureInitializedAsync();
                if (!initialized) return false;

                var json = JsonSerializer.Serialize(value, _jsonOptions);
                return await _jsRuntime.InvokeAsync<bool>(
                    "firestoreModule.removeFromArrayField", 
                    collection, 
                    docId, 
                    fieldName, 
                    json
                );
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, $"Removing from array field: {fieldName}");
                return false;
            }
        }

        // ==================== COLLECTION OPERATIONS ====================

        public async Task<List<T>> GetCollectionAsync<T>(string collection) where T : class
        {
            try
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"📚 C# GetCollectionAsync: {collection}",
                    LogLevel.Debug
                );

                var initialized = await EnsureInitializedAsync();
                if (!initialized)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "❌ Cannot get collection - Firestore not initialized",
                        LogLevel.Error
                    );
                    return new List<T>();
                }

                var jsonResult = await _jsRuntime.InvokeAsync<string>(
                    "firestoreModule.getCollection", 
                    collection
                );
                
                if (string.IsNullOrEmpty(jsonResult))
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"⚠️ Empty collection: {collection}",
                        LogLevel.Warning
                    );
                    return new List<T>();
                }
                
                var result = JsonSerializer.Deserialize<List<T>>(jsonResult, _jsonOptions);
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ Collection retrieved: {collection} ({result?.Count ?? 0} items)",
                    LogLevel.Debug
                );
                
                return result ?? new List<T>();
            }
            catch (JSException jsEx)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"❌ JavaScript error getting collection {collection}: {jsEx.Message}",
                    LogLevel.Error
                );
                return new List<T>();
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, $"Getting collection: {collection}");
                return new List<T>();
            }
        }

        public async Task<List<T>> QueryCollectionAsync<T>(string collection, string field, object value) where T : class
        {
            try
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"🔍 C# QueryCollectionAsync: {collection} where {field} == {value}",
                    LogLevel.Debug
                );

                var initialized = await EnsureInitializedAsync();
                if (!initialized)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "❌ Cannot query collection - Firestore not initialized",
                        LogLevel.Error
                    );
                    return new List<T>();
                }

                var jsonValue = JsonSerializer.Serialize(value, _jsonOptions);
                var jsonResult = await _jsRuntime.InvokeAsync<string>(
                    "firestoreModule.queryCollection", 
                    collection, 
                    field, 
                    jsonValue
                );
                
                if (string.IsNullOrEmpty(jsonResult))
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        $"⚠️ Query returned no results: {collection}",
                        LogLevel.Warning
                    );
                    return new List<T>();
                }
                
                var result = JsonSerializer.Deserialize<List<T>>(jsonResult, _jsonOptions);
                
                await MID_HelperFunctions.DebugMessageAsync(
                    $"✅ Query completed: {collection} ({result?.Count ?? 0} items)",
                    LogLevel.Debug
                );
                
                return result ?? new List<T>();
            }
            catch (JSException jsEx)
            {
                await MID_HelperFunctions.DebugMessageAsync(
                    $"❌ JavaScript error querying collection {collection}: {jsEx.Message}",
                    LogLevel.Error
                );
                return new List<T>();
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, $"Querying collection: {collection}");
                return new List<T>();
            }
        }

        public async Task<bool> AddBatchAsync<T>(string collection, List<T> items) where T : class
        {
            try
            {
                var initialized = await EnsureInitializedAsync();
                if (!initialized) return false;

                var json = JsonSerializer.Serialize(items, _jsonOptions);
                return await _jsRuntime.InvokeAsync<bool>(
                    "firestoreModule.addBatch", 
                    collection, 
                    json
                );
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, $"Adding batch to: {collection}");
                return false;
            }
        }

        // ==================== SUBCOLLECTION OPERATIONS ====================
        // Note: Removed subcollection methods since they're not in the JS module
        
        public Task<string> AddToSubcollectionAsync<T>(string collection, string docId, string subcollection, T data, string customId = null) where T : class
        {
            throw new NotImplementedException("Subcollections not implemented in current JS module");
        }

        public Task<List<T>> GetSubcollectionAsync<T>(string collection, string docId, string subcollection) where T : class
        {
            throw new NotImplementedException("Subcollections not implemented in current JS module");
        }

        public Task<T> GetSubcollectionDocumentAsync<T>(string collection, string docId, string subcollection, string subdocId) where T : class
        {
            throw new NotImplementedException("Subcollections not implemented in current JS module");
        }

        public Task<bool> UpdateSubcollectionDocumentAsync<T>(string collection, string docId, string subcollection, string subdocId, T data) where T : class
        {
            throw new NotImplementedException("Subcollections not implemented in current JS module");
        }

        public Task<bool> DeleteSubcollectionDocumentAsync(string collection, string docId, string subcollection, string subdocId)
        {
            throw new NotImplementedException("Subcollections not implemented in current JS module");
        }

        public Task<List<T>> QuerySubcollectionAsync<T>(string collection, string docId, string subcollection, string field, object value) where T : class
        {
            throw new NotImplementedException("Subcollections not implemented in current JS module");
        }

        // ==================== CONNECTION MANAGEMENT ====================

        public async Task<bool> IsConnectedAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.isConnected");
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> SetConnectionStateAsync(bool enableConnection)
        {
            try
            {
                var initialized = await EnsureInitializedAsync();
                if (!initialized) return false;

                return await _jsRuntime.InvokeAsync<bool>(
                    "firestoreModule.setConnectionState", 
                    enableConnection
                );
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, "Setting connection state");
                return false;
            }
        }

        public async Task<bool> GetManualConnectionStateAsync()
        {
            try
            {
                var initialized = await EnsureInitializedAsync();
                if (!initialized) return true;

                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.getManualConnectionState");
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, "Getting manual connection state");
                return true;
            }
        }

        public async Task ProcessPendingOperationsAsync()
        {
            try
            {
                var initialized = await EnsureInitializedAsync();
                if (!initialized) return;

                await _jsRuntime.InvokeVoidAsync("firestoreModule.processPendingOperations");
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, "Processing pending operations");
            }
        }
    }
}