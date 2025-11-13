namespace SubashaVentures.Services.Supabase;

public interface ISupabaseService
{
    // CRUD Operations - Updated constraints to match implementation
    Task<T?> GetByIdAsync<T>(string table, string id) where T : class, new();
    Task<List<T>> GetAllAsync<T>(string table) where T : class, new();
    Task<List<T>> QueryAsync<T>(string table, string filter) where T : class, new();
    Task<string?> InsertAsync<T>(string table, T data) where T : class, new();
    Task<bool> UpdateAsync<T>(string table, string id, T data) where T : class, new();
    Task<bool> DeleteAsync(string table, string id);
    
    // Batch operations
    Task<bool> InsertBatchAsync<T>(string table, List<T> items) where T : class, new();
    Task<bool> UpdateBatchAsync<T>(string table, Dictionary<string, T> updates) where T : class, new();
    
    // Raw SQL execution
    Task<T?> ExecuteScalarAsync<T>(string sql);
    Task ExecuteAsync(string sql);
}
