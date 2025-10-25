
using Microsoft.JSInterop;
using Blazored.LocalStorage;
using SubashaVentures.Utilities.HelperScripts;
using  LogLevel = SubashaVentures.Utilities.Logging.LogLevel;
namespace SubashaVentures.Services.Storage
{
    
    /// <summary>
    ///  local storage service leveraging Blazored.LocalStorage with persistent device management
    /// Optimized for SubashaVentures application with prefix-based key management and batch operations to make things easier to distinguish
    /// </summary>
    public class BlazorAppLocalStorageService : IBlazorAppLocalStorageService
    {
        private readonly ILocalStorageService _blazoredStorage;
        private readonly IJSRuntime _jsRuntime;
        private const string LocalStorageKey = "SubashaVentures_";
        
        // Persistent keys that should survive clearAll operations
        private static readonly HashSet<string> PersistentKeys = new()
        {
            "device_guid",
            "device_installation_id"
        };

        public BlazorAppLocalStorageService(ILocalStorageService blazoredStorage, IJSRuntime jsRuntime)
        {
            _blazoredStorage = blazoredStorage;
            _jsRuntime = jsRuntime;
        }

        /// <summary>
        /// Retrieve typed item from local storage with SubashaVentures prefix
        /// </summary>
        /// <typeparam name="T">Target deserialization type</typeparam>
        /// <param name="key">Storage key without prefix</param>
        /// <returns>Deserialized item or default value</returns>
        public async Task<T> GetItemAsync<T>(string key)
        {
            try
            {
                string fullKey = LocalStorageKey + key;
                
                if (!await _blazoredStorage.ContainKeyAsync(fullKey))
                    return default;

                return await _blazoredStorage.GetItemAsync<T>(fullKey);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error retrieving item '{key}': {ex.Message}", LogLevel.Exception);
                return default;
            }
        }

        /// <summary>
        /// Store typed item with automatic serialization and SubashaVentures prefix
        /// </summary>
        /// <typeparam name="T">Source serialization type</typeparam>
        /// <param name="key">Storage key without prefix</param>
        /// <param name="value">Value to serialize and store</param>
        /// <returns>Operation success status</returns>
        public async Task<bool> SetItemAsync<T>(string key, T value)
        {
            try
            {
                string fullKey = LocalStorageKey + key;
                await _blazoredStorage.SetItemAsync(fullKey, value);
                return true;
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error storing item '{key}': {ex.Message}", LogLevel.Exception);
                return false;
            }
        }

        /// <summary>
        /// Remove individual item from storage
        /// </summary>
        /// <param name="key">Storage key without prefix</param>
        /// <returns>Operation success status</returns>
        public async Task<bool> RemoveItemAsync(string key)
        {
            try
            {
                string fullKey = LocalStorageKey + key;
                await _blazoredStorage.RemoveItemAsync(fullKey);
                return true;
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error removing item '{key}': {ex.Message}", LogLevel.Exception);
                return false;
            }
        }

        /// <summary>
        /// Clear all SubashaVentures storage items while preserving persistent device identifiers
        /// Maintains device continuity across session resets
        /// </summary>
        /// <returns>Operation success status</returns>
        public async Task<bool> ClearAllAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("eval", GenerateClearAllScript());
                return true;
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error clearing storage: {ex.Message}", LogLevel.Exception);
                return false;
            }
        }

        /// <summary>
        /// Verify item existence in storage
        /// </summary>
        /// <param name="key">Storage key without prefix</param>
        /// <returns>Existence confirmation</returns>
        public async Task<bool> ContainsKeyAsync(string key)
        {
            try
            {
                string fullKey = LocalStorageKey + key;
                return await _blazoredStorage.ContainKeyAsync(fullKey);
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error checking key existence '{key}': {ex.Message}", LogLevel.Exception);
                return false;
            }
        }

        /// <summary>
        /// Calculate total storage footprint for SubashaVentures data
        /// </summary>
        /// <returns>Storage size in bytes</returns>
        public async Task<long> GetSizeAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<long>("eval", GenerateSizeCalculationScript());
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error calculating storage size: {ex.Message}", LogLevel.Exception);
                return 0;
            }
        }

        /// <summary>
        /// Batch removal of multiple storage keys with existence validation
        /// </summary>
        /// <param name="keys">Collection of keys to remove (without prefix)</param>
        /// <returns>Operation success status</returns>
        public async Task<bool> RemoveKeysAsync(IEnumerable<string> keys)
        {
            try
            {
                var removalTasks = new List<Task>();
                
                foreach (string key in keys)
                {
                    string fullKey = LocalStorageKey + key;
                    if (await _blazoredStorage.ContainKeyAsync(fullKey))
                    {
                        removalTasks.Add(_blazoredStorage.RemoveItemAsync(fullKey).AsTask());
                    }
                }

                await Task.WhenAll(removalTasks);
                return true;
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error in batch key removal: {ex.Message}", LogLevel.Exception);
                return false;
            }
        }

        /// <summary>
        /// Batch storage operation for multiple key-value pairs
        /// Optimized for bulk data initialization scenarios
        /// </summary>
        /// <typeparam name="T">Value type for serialization</typeparam>
        /// <param name="items">Dictionary of key-value pairs to store</param>
        /// <returns>Operation success status</returns>
        public async Task<bool> SetMultipleAsync<T>(Dictionary<string, T> items)
        {
            try
            {
                var storageTasks = new List<Task>();
                
                foreach (var kvp in items)
                {
                    string fullKey = LocalStorageKey + kvp.Key;
                    storageTasks.Add(_blazoredStorage.SetItemAsync(fullKey, kvp.Value).AsTask());
                }

                await Task.WhenAll(storageTasks);
                return true;
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error in batch storage operation: {ex.Message}", LogLevel.Exception);
                return false;
            }
        }

        /// <summary>
        /// Batch retrieval of multiple storage items with type safety
        /// </summary>
        /// <typeparam name="T">Target deserialization type</typeparam>
        /// <param name="keys">Collection of keys to retrieve</param>
        /// <returns>Dictionary mapping keys to retrieved values</returns>
        public async Task<Dictionary<string, T>> GetMultipleAsync<T>(IEnumerable<string> keys)
        {
            var results = new Dictionary<string, T>();
            
            try
            {
                var retrievalTasks = new List<Task<(string key, T value)>>();
                
                foreach (string key in keys)
                {
                    retrievalTasks.Add(GetItemWithKeyAsync<T>(key));
                }

                var completedTasks = await Task.WhenAll(retrievalTasks);
                
                foreach (var (key, value) in completedTasks)
                {
                    if (!EqualityComparer<T>.Default.Equals(value, default))
                    {
                        results[key] = value;
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                MID_HelperFunctions.DebugMessage($"Error in batch retrieval: {ex.Message}", LogLevel.Exception);
                return results;
            }
        }

        /// <summary>
        /// Internal helper for batch retrieval operations
        /// </summary>
        private async Task<(string key, T value)> GetItemWithKeyAsync<T>(string key)
        {
            var value = await GetItemAsync<T>(key);
            return (key, value);
        }

        /// <summary>
        /// Generate JavaScript for selective storage clearing with persistent key preservation
        /// </summary>
        private string GenerateClearAllScript()
        {
            var persistentKeysList = string.Join("', '", PersistentKeys.Select(k => LocalStorageKey + k));
            
            return $@"
                (function() {{
                    var keysToRemove = [];
                    var persistentKeys = ['{persistentKeysList}'];
                    
                    for (var i = 0; i < localStorage.length; i++) {{
                        var key = localStorage.key(i);
                        if (key && key.startsWith('{LocalStorageKey}') && 
                            !persistentKeys.includes(key)) {{
                            keysToRemove.push(key);
                        }}
                    }}
                    
                    keysToRemove.forEach(function(key) {{
                        localStorage.removeItem(key);
                    }});
                    
                    return keysToRemove.length;
                }})();
            ";
        }

        /// <summary>
        /// Generate JavaScript for storage size calculation
        /// </summary>
        private string GenerateSizeCalculationScript()
        {
            return $@"
                (function() {{
                    var totalSize = 0;
                    for (var i = 0; i < localStorage.length; i++) {{
                        var key = localStorage.key(i);
                        if (key && key.startsWith('{LocalStorageKey}')) {{
                            var value = localStorage.getItem(key);
                            totalSize += (key.length + (value ? value.length : 0)) * 2;
                        }}
                    }}
                    return totalSize;
                }})();
            ";
        }
    }
}