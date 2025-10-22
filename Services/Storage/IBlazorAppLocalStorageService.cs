
namespace SubashaVentures.Services.Storage
{
    /// <summary>
    /// Interface for local storage service with enhanced batch operations
    /// </summary>
    public interface IBlazorAppLocalStorageService
    {
        Task<T> GetItemAsync<T>(string key);
        Task<bool> SetItemAsync<T>(string key, T value);
        Task<bool> RemoveItemAsync(string key);
        Task<bool> ClearAllAsync();
        Task<bool> ContainsKeyAsync(string key);
        Task<long> GetSizeAsync();
        
        // Enhanced batch operations
        Task<bool> RemoveKeysAsync(IEnumerable<string> keys);
        Task<bool> SetMultipleAsync<T>(Dictionary<string, T> items);
        Task<Dictionary<string, T>> GetMultipleAsync<T>(IEnumerable<string> keys);
    }
}