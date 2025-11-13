// Services/Firebase/FirestoreService.cs - FIXED: Prevent multiple initializations
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

        private async Task InitializeAsync()
        {
            // CRITICAL FIX: Prevent multiple initializations
            if (_isInitialized || _isInitializing)
            {
                return;
            }

            await _initLock.WaitAsync();
            
            try
            {
                // Double-check after acquiring lock
                if (_isInitialized || _isInitializing)
                {
                    return;
                }

                _isInitializing = true;

                // Check if already initialized via JavaScript
                var alreadyInitialized = await _jsRuntime.InvokeAsync<bool>(
                    "eval", 
                    "typeof window.firestoreModule !== 'undefined' && window.firestoreModule.isConnected !== undefined"
                );

                if (alreadyInitialized)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "Firestore already initialized in JavaScript", 
                        LogLevel.Info
                    );
                    _isInitialized = true;
                    IsConfigured = true;
                    return;
                }

                // Initialize through JavaScript module
                _isInitialized = await _jsRuntime.InvokeAsync<bool>("firestoreModule.initializeFirestore");
                IsConfigured = _isInitialized;
                
                if (_isInitialized)
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "✓ Firestore initialized successfully", 
                        LogLevel.Info
                    );
                }
                else
                {
                    await MID_HelperFunctions.DebugMessageAsync(
                        "❌ Failed to initialize Firestore", 
                        LogLevel.Error
                    );
                }
            }
            catch (Exception ex)
            {
                await MID_HelperFunctions.LogExceptionAsync(ex, "Initializing Firestore");
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
                if (!_isInitialized) await InitializeAsync();
                var jsonResult = await _jsRuntime.InvokeAsync<string>("firestoreModule.getDocument", collection, id);
                
                if (string.IsNullOrEmpty(jsonResult))
                    return null;
                
                return JsonSerializer.Deserialize<T>(jsonResult, _jsonOptions);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error getting document: {ex.Message}", LogLevel.Exception);
                return null;
            }
        }

        public async Task<string> AddDocumentAsync<T>(string collection, T data, string customId = null) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                return await _jsRuntime.InvokeAsync<string>("firestoreModule.addDocument", collection, json, customId);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error adding document: {ex.Message}", LogLevel.Exception);
                return null;
            }
        }

        public async Task<bool> UpdateDocumentAsync<T>(string collection, string id, T data) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.updateDocument", collection, id, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error updating document: {ex.Message}", LogLevel.Exception);
                return false;
            }
        }

        public async Task<bool> DeleteDocumentAsync(string collection, string id)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.deleteDocument", collection, id);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error deleting document: {ex.Message}", LogLevel.Exception);
                return false;
            }
        }

        // ==================== FIELD OPERATIONS ====================

        public async Task<bool> AddOrUpdateFieldAsync<T>(string collection, string docId, string fieldName, T value)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonSerializer.Serialize(value, _jsonOptions);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.addOrUpdateField", collection, docId, fieldName, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error updating field: {ex.Message}", LogLevel.Exception);
                return false;
            }
        }

        public async Task<bool> UpdateFieldsAsync<T>(string collection, string docId, T fields) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonSerializer.Serialize(fields, _jsonOptions);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.updateFields", collection, docId, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error updating fields: {ex.Message}", LogLevel.Exception);
                return false;
            }
        }

        public async Task<bool> RemoveFieldAsync(string collection, string docId, string fieldName)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.removeField", collection, docId, fieldName);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error removing field: {ex.Message}", LogLevel.Exception);
                return false;
            }
        }

        public async Task<bool> RemoveFieldsAsync(string collection, string docId, List<string> fieldNames)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonSerializer.Serialize(fieldNames, _jsonOptions);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.removeFields", collection, docId, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error removing fields: {ex.Message}", LogLevel.Exception);
                return false;
            }
        }

        public async Task<T> GetFieldAsync<T>(string collection, string docId, string fieldName)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var jsonResult = await _jsRuntime.InvokeAsync<string>("firestoreModule.getField", collection, docId, fieldName);
                
                if (string.IsNullOrEmpty(jsonResult))
                    return default(T);
                
                return JsonSerializer.Deserialize<T>(jsonResult, _jsonOptions);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error getting field: {ex.Message}", LogLevel.Exception);
                return default(T);
            }
        }
       
        // ==================== SUBCOLLECTION OPERATIONS ====================

        public async Task<string> AddToSubcollectionAsync<T>(string collection, string docId, string subcollection, T data, string customId = null) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                return await _jsRuntime.InvokeAsync<string>("firestoreModule.addToSubcollection", collection, docId, subcollection, json, customId);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error adding to subcollection: {ex.Message}", LogLevel.Exception);
                return null;
            }
        }

        public async Task<List<T>> GetSubcollectionAsync<T>(string collection, string docId, string subcollection) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var jsonResult = await _jsRuntime.InvokeAsync<string>("firestoreModule.getSubcollection", collection, docId, subcollection);
                
                if (string.IsNullOrEmpty(jsonResult))
                    return new List<T>();
                
                return JsonSerializer.Deserialize<List<T>>(jsonResult, _jsonOptions);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error getting subcollection: {ex.Message}", LogLevel.Exception);
                return new List<T>();
            }
        }

        public async Task<T> GetSubcollectionDocumentAsync<T>(string collection, string docId, string subcollection, string subdocId) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var jsonResult = await _jsRuntime.InvokeAsync<string>("firestoreModule.getSubcollectionDocument", collection, docId, subcollection, subdocId);
                
                if (string.IsNullOrEmpty(jsonResult))
                    return null;
                
                return JsonSerializer.Deserialize<T>(jsonResult, _jsonOptions);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error getting subcollection document: {ex.Message}", LogLevel.Exception);
                return null;
            }
        }

        public async Task<bool> UpdateSubcollectionDocumentAsync<T>(string collection, string docId, string subcollection, string subdocId, T data) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.updateSubcollectionDocument", collection, docId, subcollection, subdocId, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error updating subcollection document: {ex.Message}", LogLevel.Exception);
                return false;
            }
        }

        public async Task<bool> DeleteSubcollectionDocumentAsync(string collection, string docId, string subcollection, string subdocId)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.deleteSubcollectionDocument", collection, docId, subcollection, subdocId);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error deleting subcollection document: {ex.Message}", LogLevel.Exception);
                return false;
            }
        }

        public async Task<List<T>> QuerySubcollectionAsync<T>(string collection, string docId, string subcollection, string field, object value) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var jsonValue = JsonSerializer.Serialize(value, _jsonOptions);
                var jsonResult = await _jsRuntime.InvokeAsync<string>("firestoreModule.querySubcollection", collection, docId, subcollection, field, jsonValue);
                
                if (string.IsNullOrEmpty(jsonResult))
                    return new List<T>();
                
                return JsonSerializer.Deserialize<List<T>>(jsonResult, _jsonOptions);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error querying subcollection: {ex.Message}", LogLevel.Exception);
                return new List<T>();
            }
        }

        // ==================== ARRAY FIELD OPERATIONS ====================

        public async Task<bool> AddToArrayFieldAsync<T>(string collection, string docId, string fieldName, T value)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonSerializer.Serialize(value, _jsonOptions);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.addToArrayField", collection, docId, fieldName, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error adding to array field: {ex.Message}", LogLevel.Exception);
                return false;
            }
        }

        public async Task<bool> RemoveFromArrayFieldAsync<T>(string collection, string docId, string fieldName, T value)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonSerializer.Serialize(value, _jsonOptions);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.removeFromArrayField", collection, docId, fieldName, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error removing from array field: {ex.Message}", LogLevel.Exception);
                return false;
            }
        }

        // ==================== COLLECTION OPERATIONS ====================

        public async Task<List<T>> GetCollectionAsync<T>(string collection) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var jsonResult = await _jsRuntime.InvokeAsync<string>("firestoreModule.getCollection", collection);
                
                if (string.IsNullOrEmpty(jsonResult))
                    return new List<T>();
                
                return JsonSerializer.Deserialize<List<T>>(jsonResult, _jsonOptions);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error getting collection: {ex.Message}", LogLevel.Exception);
                return new List<T>();
            }
        }

        public async Task<List<T>> QueryCollectionAsync<T>(string collection, string field, object value) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var jsonValue = JsonSerializer.Serialize(value, _jsonOptions);
                var jsonResult = await _jsRuntime.InvokeAsync<string>("firestoreModule.queryCollection", collection, field, jsonValue);
                
                if (string.IsNullOrEmpty(jsonResult))
                    return new List<T>();
                
                return JsonSerializer.Deserialize<List<T>>(jsonResult, _jsonOptions);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error querying collection: {ex.Message}", LogLevel.Exception);
                return new List<T>();
            }
        }

        public async Task<bool> AddBatchAsync<T>(string collection, List<T> items) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonSerializer.Serialize(items, _jsonOptions);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.addBatch", collection, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error adding batch: {ex.Message}", LogLevel.Exception);
                return false;
            }
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
                if (!_isInitialized) await InitializeAsync();
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.setConnectionState", enableConnection);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error setting connection state: {ex.Message}", LogLevel.Exception);
                return false;
            }
        }

        public async Task<bool> GetManualConnectionStateAsync()
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.getManualConnectionState");
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error getting manual connection state: {ex.Message}", LogLevel.Exception);
                return true;
            }
        }

        public async Task ProcessPendingOperationsAsync()
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                await _jsRuntime.InvokeVoidAsync("firestoreModule.processPendingOperations");
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error processing pending operations: {ex.Message}", LogLevel.Exception);
            }
        }
    }
}