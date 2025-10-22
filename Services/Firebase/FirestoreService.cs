// Services/Firebase/FirestoreService.cs
using SubashaVentures.Domain.Enums;
using SubashaVentures.Models.Firebase;
using SubashaVentures.Services.Storage;
using Microsoft.JSInterop;
using SubashaVentures.Utilities.HelperScripts;
using Newtonsoft.Json;
namespace SubashaVentures.Services.Firebase
{
    /// <summary>
    /// Service for Firestore database operations using JavaScript interop
    /// </summary>
    public class FirestoreService : IFirestoreService
    {
        private readonly IJSRuntime _jsRuntime;
        private bool _isInitialized = false;
        
        private readonly IBlazorAppLocalStorageService _localStorage;

        // Document size tracking (approximate)
        private const int MAX_DOCUMENT_SIZE_BYTES = 900000; // 900KB to leave buffer
        private const int ESTIMATED_STUDENT_ENTRY_SIZE = 2000; // ~2KB per student entry
        private const int ESTIMATED_ATTENDANCE_ENTRY_SIZE = 500; // ~500B per attendance


        public bool IsInitialized => _isInitialized;
        public bool IsConfigured { get; private set; }

        private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };

        public FirestoreService(IJSRuntime jsRuntime,IBlazorAppLocalStorageService localStorage,IPermissionService permissionService, IAuthService authService)
        {
            _jsRuntime = jsRuntime;
            _localStorage = localStorage ??
                            throw new NullReferenceException("Missing localStorage service refrence");
            _permissionService = permissionService ??
                                 throw new NullReferenceException("Missing permission service refrence");
            _authService = authService ??
                                 throw new NullReferenceException("Missing auth service refrence");
            InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                if (IsConfigured) return;

                _isInitialized = await _jsRuntime.InvokeAsync<bool>("firestoreModule.initializeFirestore");
                IsConfigured = _isInitialized;
                
                if (_isInitialized)
                {
                    MID_HelperFunctions.DebugMessage("Firestore initialized successfully", DebugClass.Info);
                }
                else
                {
                    MID_HelperFunctions.DebugMessage("Failed to initialize Firestore", DebugClass.Error);
                }
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error initializing Firestore: {ex.Message}", DebugClass.Exception);
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
                
                return JsonConvert.DeserializeObject<T>(jsonResult, _jsonSettings);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error getting document: {ex.Message}", DebugClass.Exception);
                return null;
            }
        }

        public async Task<string> AddDocumentAsync<T>(string collection, T data, string customId = null) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonConvert.SerializeObject(data, _jsonSettings);
                return await _jsRuntime.InvokeAsync<string>("firestoreModule.addDocument", collection, json, customId);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error adding document: {ex.Message}", DebugClass.Exception);
                return null;
            }
        }

        public async Task<bool> UpdateDocumentAsync<T>(string collection, string id, T data) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonConvert.SerializeObject(data, _jsonSettings);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.updateDocument", collection, id, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error updating document: {ex.Message}", DebugClass.Exception);
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
                MID_HelperFunctions.DebugMessage($"Error deleting document: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        // ==================== FIELD OPERATIONS ====================

        public async Task<bool> AddOrUpdateFieldAsync<T>(string collection, string docId, string fieldName, T value)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonConvert.SerializeObject(value, _jsonSettings);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.addOrUpdateField", collection, docId, fieldName, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error updating field: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        public async Task<bool> UpdateFieldsAsync<T>(string collection, string docId, T fields) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonConvert.SerializeObject(fields, _jsonSettings);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.updateFields", collection, docId, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error updating fields: {ex.Message}", DebugClass.Exception);
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
                MID_HelperFunctions.DebugMessage($"Error removing field: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        public async Task<bool> RemoveFieldsAsync(string collection, string docId, List<string> fieldNames)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonConvert.SerializeObject(fieldNames, _jsonSettings);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.removeFields", collection, docId, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error removing fields: {ex.Message}", DebugClass.Exception);
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
                
                return JsonConvert.DeserializeObject<T>(jsonResult, _jsonSettings);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error getting field: {ex.Message}", DebugClass.Exception);
                return default(T);
            }
        }
       
        // ==================== SUBCOLLECTION OPERATIONS ====================

        public async Task<string> AddToSubcollectionAsync<T>(string collection, string docId, string subcollection, T data, string customId = null) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonConvert.SerializeObject(data, _jsonSettings);
                return await _jsRuntime.InvokeAsync<string>("firestoreModule.addToSubcollection", collection, docId, subcollection, json, customId);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error adding to subcollection: {ex.Message}", DebugClass.Exception);
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
                
                return JsonConvert.DeserializeObject<List<T>>(jsonResult, _jsonSettings);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error getting subcollection: {ex.Message}", DebugClass.Exception);
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
                
                return JsonConvert.DeserializeObject<T>(jsonResult, _jsonSettings);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error getting subcollection document: {ex.Message}", DebugClass.Exception);
                return null;
            }
        }

        public async Task<bool> UpdateSubcollectionDocumentAsync<T>(string collection, string docId, string subcollection, string subdocId, T data) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonConvert.SerializeObject(data, _jsonSettings);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.updateSubcollectionDocument", collection, docId, subcollection, subdocId, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error updating subcollection document: {ex.Message}", DebugClass.Exception);
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
                MID_HelperFunctions.DebugMessage($"Error deleting subcollection document: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        public async Task<List<T>> QuerySubcollectionAsync<T>(string collection, string docId, string subcollection, string field, object value) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var jsonValue = JsonConvert.SerializeObject(value, _jsonSettings);
                var jsonResult = await _jsRuntime.InvokeAsync<string>("firestoreModule.querySubcollection", collection, docId, subcollection, field, jsonValue);
                
                if (string.IsNullOrEmpty(jsonResult))
                    return new List<T>();
                
                return JsonConvert.DeserializeObject<List<T>>(jsonResult, _jsonSettings);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error querying subcollection: {ex.Message}", DebugClass.Exception);
                return new List<T>();
            }
        }

        // ==================== ARRAY FIELD OPERATIONS ====================

        public async Task<bool> AddToArrayFieldAsync<T>(string collection, string docId, string fieldName, T value)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonConvert.SerializeObject(value, _jsonSettings);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.addToArrayField", collection, docId, fieldName, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error adding to array field: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        public async Task<bool> RemoveFromArrayFieldAsync<T>(string collection, string docId, string fieldName, T value)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonConvert.SerializeObject(value, _jsonSettings);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.removeFromArrayField", collection, docId, fieldName, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error removing from array field: {ex.Message}", DebugClass.Exception);
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
                
                return JsonConvert.DeserializeObject<List<T>>(jsonResult, _jsonSettings);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error getting collection: {ex.Message}", DebugClass.Exception);
                return new List<T>();
            }
        }

        public async Task<List<T>> QueryCollectionAsync<T>(string collection, string field, object value) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var jsonValue = JsonConvert.SerializeObject(value, _jsonSettings);
                var jsonResult = await _jsRuntime.InvokeAsync<string>("firestoreModule.queryCollection", collection, field, jsonValue);
                
                if (string.IsNullOrEmpty(jsonResult))
                    return new List<T>();
                
                return JsonConvert.DeserializeObject<List<T>>(jsonResult, _jsonSettings);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error querying collection: {ex.Message}", DebugClass.Exception);
                return new List<T>();
            }
        }

        public async Task<bool> AddBatchAsync<T>(string collection, List<T> items) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonConvert.SerializeObject(items, _jsonSettings);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.addBatch", collection, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error adding batch: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        // ==================== LEGACY/SPECIALIZED OPERATIONS ====================

        public async Task<bool> FindAndDeleteCourseAsync(string courseCode)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.findAndDeleteCourse", courseCode);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error finding and deleting course {courseCode}: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        public async Task<bool> DeleteFromSpecificCollectionAsync(string collection, string courseCode)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.deleteFromSpecificCollection", collection, courseCode);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error deleting course {courseCode} from {collection}: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }

        public async Task<bool> SyncCollectionWithLocalAsync<T>(string collection, List<T> localData) where T : class
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                var json = JsonConvert.SerializeObject(localData, _jsonSettings);
                return await _jsRuntime.InvokeAsync<bool>("firestoreModule.syncCollectionWithLocal", collection, json);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error syncing collection: {ex.Message}", DebugClass.Exception);
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
                MID_HelperFunctions.DebugMessage($"Error setting connection state: {ex.Message}", DebugClass.Exception);
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
                MID_HelperFunctions.DebugMessage($"Error getting manual connection state: {ex.Message}", DebugClass.Exception);
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
                MID_HelperFunctions.DebugMessage($"Error processing pending operations: {ex.Message}", DebugClass.Exception);
            }
        }
        
        #region Student Level Operations
        
        private const string STUDENTS_LEVEL_COLLECTION = "STUDENTS_LEVEL_TRACKER";
        private const string LEVEL_CACHE_KEY = "student_levels_cache";
        private const string BASE_LEVEL_DOCUMENT = "STUDENT_LEVELS_NO";

        public async Task<LevelType> GetStudentLevelType(string matricNumber)
        {
            try
            {
                var levelData = await GetStudentLevelAsync(matricNumber);
                if (levelData == null || string.IsNullOrEmpty(levelData.Level))
                {
                    MID_HelperFunctions.DebugMessage($"No level data found for {matricNumber}", DebugClass.Warning);
                    return LevelType.LevelExtra;
                }

                var levelString = levelData.Level.Trim();
        
                MID_HelperFunctions.DebugMessage($"Student {matricNumber} level string: '{levelString}'", DebugClass.Info);
        
                return levelString switch
                {
                    "100" => LevelType.Level100,
                    "200" => LevelType.Level200,
                    "300" => LevelType.Level300,
                    "400" => LevelType.Level400,
                    "500" => LevelType.Level500,
                    _ => LevelType.LevelExtra
                };
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error getting student level for {matricNumber}: {ex.Message}", DebugClass.Exception);
                return LevelType.LevelExtra;
            }
        }
        public async Task<StudentLevelInfo> GetStudentLevelAsync(string matricNumber)
{
    try
    {
        if (!_isInitialized) await InitializeAsync();
        
        MID_HelperFunctions.DebugMessage($"Getting student level for: {matricNumber}", DebugClass.Info);
        
        // ONLINE FIRST - Find document containing this matric number
        string targetDocument = await FindDocumentContainingKeyAsync(
            STUDENTS_LEVEL_COLLECTION, BASE_LEVEL_DOCUMENT, matricNumber);
        
        if (string.IsNullOrEmpty(targetDocument))
        {
            MID_HelperFunctions.DebugMessage($"No document found containing matric number: {matricNumber}", DebugClass.Warning);
            
            // Try offline cache as fallback
            var cachedLevels = await _localStorage?.GetItemAsync<Dictionary<string, StudentLevelInfo>>(LEVEL_CACHE_KEY);
            if (cachedLevels?.ContainsKey(matricNumber) == true)
            {
                MID_HelperFunctions.DebugMessage($"Found student level in offline cache: {matricNumber}", DebugClass.Info);
                return cachedLevels[matricNumber];
            }
            
            return null;
        }
        
        MID_HelperFunctions.DebugMessage($"Found student in document: {targetDocument}", DebugClass.Info);
        
        // Get student data from the found document
        var studentData = await GetFieldAsync<Dictionary<string, object>>(
            STUDENTS_LEVEL_COLLECTION, targetDocument, matricNumber);
        
        if (studentData == null)
        {
            MID_HelperFunctions.DebugMessage($"Student data is null in document: {targetDocument}", DebugClass.Warning);
            return null;
        }
        
        var studentInfo = new StudentLevelInfo
        {
            MatricNumber = matricNumber,
            Email = studentData.GetValueOrDefault("email")?.ToString(),
            Level = studentData.GetValueOrDefault("level")?.ToString(),
            RegistrationDate = DateTime.TryParse(studentData.GetValueOrDefault("registrationDate")?.ToString(), out var date) 
                ? date : DateTime.MinValue
        };
        
        MID_HelperFunctions.DebugMessage($"Retrieved student info - Level: {studentInfo.Level}, Email: {studentInfo.Email}", DebugClass.Info);
        
        // Cache locally after successful online retrieval
        await CacheStudentLevelAsync(matricNumber, studentInfo);
        
        return studentInfo;
    }
    catch (Exception ex)
    {
        MID_HelperFunctions.DebugMessage($"Error getting student level for {matricNumber}: {ex.Message}", DebugClass.Exception);
        
        // Try offline cache as fallback on error
        try
        {
            var cachedLevels = await _localStorage?.GetItemAsync<Dictionary<string, StudentLevelInfo>>(LEVEL_CACHE_KEY);
            if (cachedLevels?.ContainsKey(matricNumber) == true)
            {
                MID_HelperFunctions.DebugMessage($"Fallback to offline cache successful for: {matricNumber}", DebugClass.Info);
                return cachedLevels[matricNumber];
            }
        }
        catch (Exception cacheEx)
        {
            MID_HelperFunctions.DebugMessage($"Cache fallback also failed: {cacheEx.Message}", DebugClass.Exception);
        }
        
        return null;
    }
}

        
       public async Task<bool> UpdateStudentLevelAsync(string matricNumber, string newLevel)
{
    try
    {
        if (!_isInitialized) await InitializeAsync();

        // Find document containing this student
        string targetDocument = await FindDocumentContainingKeyAsync(
            STUDENTS_LEVEL_COLLECTION, BASE_LEVEL_DOCUMENT, matricNumber);

        if (string.IsNullOrEmpty(targetDocument))
        {
            MID_HelperFunctions.DebugMessage($"Cannot update level - student {matricNumber} not found in any document", DebugClass.Warning);
            return false;
        }

        // Update level field only
        var updateData = new Dictionary<string, object> { { "level", newLevel } };
        var json = JsonConvert.SerializeObject(updateData, _jsonSettings);

        bool result = await UpdateFieldInDistributedDocumentAsync(
            STUDENTS_LEVEL_COLLECTION, targetDocument, matricNumber, json);

        if (result)
        {
            MID_HelperFunctions.DebugMessage($"Successfully updated level for {matricNumber} to {newLevel}", DebugClass.Info);
            
            if (_permissionService.CanCacheAllStudentsLevel(await _authService.GetUserIdAsync()).Result)
            {
                // Update local cache
                var cachedLevels =
                    await _localStorage?.GetItemAsync<Dictionary<string, StudentLevelInfo>>(LEVEL_CACHE_KEY)
                    ?? new Dictionary<string, StudentLevelInfo>();

                if (cachedLevels.ContainsKey(matricNumber))
                {
                    cachedLevels[matricNumber].Level = newLevel;
                    await _localStorage?.SetItemAsync(LEVEL_CACHE_KEY, cachedLevels);
                    await _localStorage?.SetItemAsync("M_Personal_Level", cachedLevels[matricNumber].Level);
                    MID_HelperFunctions.DebugMessage($"Updated cache for {matricNumber}", DebugClass.Info);
                }
            }
        }
        else
        {
            MID_HelperFunctions.DebugMessage($"Failed to update level for {matricNumber}", DebugClass.Error);
        }

        return result;
    }
    catch (Exception ex)
    {
        MID_HelperFunctions.DebugMessage($"Error updating student level for {matricNumber}: {ex.Message}", DebugClass.Exception);
        return false;
    }
}
        
        public async Task<bool> BatchUpdateStudentLevelsAsync(Dictionary<string, string> matricToLevelMap)
        {
            try
            {
                if (!_isInitialized) await InitializeAsync();
                
                var successCount = 0;
                foreach (var kvp in matricToLevelMap)
                {
                    if (await UpdateStudentLevelAsync(kvp.Key, kvp.Value))
                    {
                        successCount++;
                    }
                }
                
                return successCount == matricToLevelMap.Count;
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error batch updating student levels: {ex.Message}", DebugClass.Exception);
                return false;
            }
        }
        
        private async Task CacheStudentLevelAsync(string matricNumber, StudentLevelInfo studentInfo)
        {
            try
            {
                if (_localStorage == null) return;
        
                if (_permissionService.CanCacheAllStudentsLevel(await _authService.GetUserIdAsync()).Result)
                {
                    var cachedLevels = await _localStorage.GetItemAsync<Dictionary<string, StudentLevelInfo>>(LEVEL_CACHE_KEY) 
                                       ?? new Dictionary<string, StudentLevelInfo>();
            
                    cachedLevels[matricNumber] = studentInfo;
                    await _localStorage.SetItemAsync(LEVEL_CACHE_KEY, cachedLevels);
            
                    MID_HelperFunctions.DebugMessage($"Cached student level for {matricNumber}", DebugClass.Info);
                }
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error caching student level: {ex.Message}", DebugClass.Exception);
            }
        }

        
        #endregion
        
        #region Distributed Storage
        
        // ==================== DISTRIBUTED DOCUMENT OPERATIONS ====================

    /// <summary>
    /// Add attendance event with automatic document distribution
    /// </summary>
    public async Task<string> AddAttendanceEventAsync(string courseCode, object attendanceData)
    {
        try
        {
            if (!_isInitialized) await InitializeAsync();
            
            string baseDocumentId = $"attendanceevent{courseCode.ToUpper()}";
            string targetDocumentId = await FindAvailableDocumentAsync("AttendanceEvents", baseDocumentId, ESTIMATED_ATTENDANCE_ENTRY_SIZE);
            
            var eventId = Guid.NewGuid().ToString();
            var json = JsonConvert.SerializeObject(attendanceData, _jsonSettings);
            return await _jsRuntime.InvokeAsync<string>("firestoreModule.addToDistributedDocument", 
                "AttendanceEvents", targetDocumentId, eventId, json);
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error adding attendance event: {ex.Message}", DebugClass.Exception);
            return null;
        }
    }


    /// <summary>
    /// Get all attendance events for a course (across all distributed documents)
    /// </summary>
    public async Task<List<T>> GetAllAttendanceEventsAsync<T>(string courseCode) where T : class
    {
        try
        {
            if (!_isInitialized) await InitializeAsync();
            
            string baseDocumentId = $"attendanceevent{courseCode.ToUpper()}";
            var allDocuments = await GetDistributedDocumentsAsync("AttendanceEvents", baseDocumentId);
            
            var combinedEvents = new List<T>();
            
            foreach (var docData in allDocuments)
            {
                var eventData = JsonConvert.DeserializeObject<Dictionary<string, T>>(docData, _jsonSettings);
                if (eventData != null)
                {
                    combinedEvents.AddRange(eventData.Values);
                }
            }
            
            return combinedEvents;
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error getting attendance events: {ex.Message}", DebugClass.Exception);
            return new List<T>();
        }
    }


    // ==================== HELPER METHODS ====================

    /// <summary>
    /// Find an available document that can accommodate new data
    /// </summary>
    private async Task<string> FindAvailableDocumentAsync(string collection, string baseDocumentId, int estimatedDataSize)
    {
        try
        {
            int documentIndex = 1;
            string currentDocumentId = baseDocumentId;
            
            while (true)
            {
                var documentSizeInfo = await _jsRuntime.InvokeAsync<string>("firestoreModule.getDocumentSizeInfo", collection, currentDocumentId);
                
                if (string.IsNullOrEmpty(documentSizeInfo))
                {
                    // Document doesn't exist, use this one
                    return currentDocumentId;
                }
                
                var sizeInfo = JsonConvert.DeserializeObject<DocumentSizeInfo>(documentSizeInfo);
                if (sizeInfo.EstimatedSize + estimatedDataSize < MAX_DOCUMENT_SIZE_BYTES)
                {
                    return currentDocumentId;
                }
                
                // Try next document
                documentIndex++;
                currentDocumentId = $"{baseDocumentId}_{documentIndex}";
            }
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error finding available document: {ex.Message}", DebugClass.Exception);
            return baseDocumentId; // Fallback to base document
        }
    }

    /// <summary>
    /// Get all distributed documents for a base document ID
    /// </summary>
    private async Task<List<string>> GetDistributedDocumentsAsync(string collection, string baseDocumentId)
    {
        try
        {
            var documents = new List<string>();
            int documentIndex = 1;
            string currentDocumentId = baseDocumentId;
            
            // Get base document
            var baseData = await _jsRuntime.InvokeAsync<string>("firestoreModule.getDocument", collection, baseDocumentId);
            if (!string.IsNullOrEmpty(baseData))
            {
                documents.Add(baseData);
            }
            
            // Get additional documents
            while (true)
            {
                currentDocumentId = $"{baseDocumentId}_{documentIndex}";
                var documentData = await _jsRuntime.InvokeAsync<string>("firestoreModule.getDocument", collection, currentDocumentId);
                
                if (string.IsNullOrEmpty(documentData))
                {
                    break; // No more documents
                }
                
                documents.Add(documentData);
                documentIndex++;
            }
            
            return documents;
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error getting distributed documents: {ex.Message}", DebugClass.Exception);
            return new List<string>();
        }
    }

    /// <summary>
/// Find which distributed document contains a specific key - FIXED VERSION
/// </summary>
private async Task<string> FindDocumentContainingKeyAsync(string collection, string baseDocumentId, string key)
{
    try
    {
        MID_HelperFunctions.DebugMessage($"Searching for key '{key}' in collection '{collection}' with base '{baseDocumentId}'", DebugClass.Info);
        
        // Check base document first (STUDENT_LEVELS_NO1)
        string currentDocumentId = $"{baseDocumentId}1";
        
        var hasKey = await _jsRuntime.InvokeAsync<bool>("firestoreModule.documentContainsKey", collection, currentDocumentId, key);
        
        if (hasKey)
        {
            MID_HelperFunctions.DebugMessage($"Found key '{key}' in document: {currentDocumentId}", DebugClass.Info);
            return currentDocumentId;
        }
        
        // Check if document exists
        var exists = await _jsRuntime.InvokeAsync<bool>("firestoreModule.documentExists", collection, currentDocumentId);
        if (!exists)
        {
            MID_HelperFunctions.DebugMessage($"Base document {currentDocumentId} does not exist", DebugClass.Warning);
            return null; // No documents exist
        }
        
        // Continue checking additional documents (NO2, NO3, etc.)
        int documentIndex = 2;
        
        while (true)
        {
            currentDocumentId = $"{baseDocumentId}{documentIndex}";
            
            MID_HelperFunctions.DebugMessage($"Checking document: {currentDocumentId}", DebugClass.Info);
            
            hasKey = await _jsRuntime.InvokeAsync<bool>("firestoreModule.documentContainsKey", collection, currentDocumentId, key);
            
            if (hasKey)
            {
                MID_HelperFunctions.DebugMessage($"Found key '{key}' in document: {currentDocumentId}", DebugClass.Info);
                return currentDocumentId;
            }
            
            // Check if document exists
            exists = await _jsRuntime.InvokeAsync<bool>("firestoreModule.documentExists", collection, currentDocumentId);
            if (!exists)
            {
                MID_HelperFunctions.DebugMessage($"Document {currentDocumentId} does not exist, ending search", DebugClass.Info);
                break; // No more documents to check
            }
            
            documentIndex++;
            
            // Safety check to prevent infinite loop
            if (documentIndex > 100) // Reasonable limit
            {
                MID_HelperFunctions.DebugMessage($"Reached maximum document search limit (100) for key: {key}", DebugClass.Warning);
                break;
            }
        }
        
        MID_HelperFunctions.DebugMessage($"Key '{key}' not found in any document", DebugClass.Warning);
        return null; // Key not found in any document
    }
    catch (Exception ex)
    {
        MID_HelperFunctions.DebugMessage($"Error finding document containing key '{key}': {ex.Message}", DebugClass.Exception);
        return null;
    }
}

    /// <summary>
    /// Get data from distributed documents by key
    /// </summary>
    private async Task<T> GetFromDistributedDocumentsAsync<T>(string collection, string baseDocumentId, string key) where T : class
    {
        try
        {
            string targetDocumentId = await FindDocumentContainingKeyAsync(collection, baseDocumentId, key);
            
            if (string.IsNullOrEmpty(targetDocumentId))
            {
                return null;
            }
            
            return await GetFieldAsync<T>(collection, targetDocumentId, key);
        }
        catch (Exception ex)
        {
            MID_HelperFunctions.DebugMessage($"Error getting from distributed documents: {ex.Message}", DebugClass.Exception);
            return null;
        }
    }
        
        #endregion
        
#region Distributed Document Operations

public async Task<string> AddToDistributedDocumentAsync(string collection, string documentId, string key, string jsonData)
{
    try
    {
        if (!_isInitialized) await InitializeAsync();
        
        return await _jsRuntime.InvokeAsync<string>("firestoreModule.addToDistributedDocument", 
            collection, documentId, key, jsonData);
    }
    catch (Exception ex)
    {
        MID_HelperFunctions.DebugMessage($"Error adding to distributed document: {ex.Message}", DebugClass.Exception);
        return null;
    }
}

public async Task<bool> UpdateFieldInDistributedDocumentAsync(string collection, string documentId, string key, string jsonData)
{
    try
    {
        if (!_isInitialized) await InitializeAsync();
        
        return await _jsRuntime.InvokeAsync<bool>("firestoreModule.updateFieldInDistributedDocument", 
            collection, documentId, key, jsonData);
    }
    catch (Exception ex)
    {
        MID_HelperFunctions.DebugMessage($"Error updating field in distributed document: {ex.Message}", DebugClass.Exception);
        return false;
    }
}

public async Task<string> GetDocumentSizeInfoAsync(string collection, string documentId)
{
    try
    {
        if (!_isInitialized) await InitializeAsync();
        
        return await _jsRuntime.InvokeAsync<string>("firestoreModule.getDocumentSizeInfo", collection, documentId);
    }
    catch (Exception ex)
    {
        MID_HelperFunctions.DebugMessage($"Error getting document size info: {ex.Message}", DebugClass.Exception);
        return null;
    }
}

public async Task<bool> DocumentContainsKeyAsync(string collection, string documentId, string key)
{
    try
    {
        if (!_isInitialized) await InitializeAsync();
        
        return await _jsRuntime.InvokeAsync<bool>("firestoreModule.documentContainsKey", collection, documentId, key);
    }
    catch (Exception ex)
    {
        MID_HelperFunctions.DebugMessage($"Error checking if document contains key: {ex.Message}", DebugClass.Exception);
        return false;
    }
}

public async Task<bool> DocumentExistsAsync(string collection, string documentId)
{
    try
    {
        if (!_isInitialized) await InitializeAsync();
        
        return await _jsRuntime.InvokeAsync<bool>("firestoreModule.documentExists", collection, documentId);
    }
    catch (Exception ex)
    {
        MID_HelperFunctions.DebugMessage($"Error checking if document exists: {ex.Message}", DebugClass.Exception);
        return false;
    }
}

#endregion
        
    }
}