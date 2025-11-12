namespace SubashaVentures.Services.Supabase;

public interface ISupabaseService
{
    // CRUD Operations
    Task<T?> GetByIdAsync<T>(string table, string id) where T : class;
    Task<List<T>> GetAllAsync<T>(string table) where T : class;
    Task<List<T>> QueryAsync<T>(string table, string filter) where T : class;
    Task<string?> InsertAsync<T>(string table, T data) where T : class;
    Task<bool> UpdateAsync<T>(string table, string id, T data) where T : class;
    Task<bool> DeleteAsync(string table, string id);
    
    // Batch operations
    Task<bool> InsertBatchAsync<T>(string table, List<T> items) where T : class;
    Task<bool> UpdateBatchAsync<T>(string table, Dictionary<string, T> updates) where T : class;
    
    // Raw SQL execution
    Task<T?> ExecuteScalarAsync<T>(string sql);
    Task ExecuteAsync(string sql);
}
