using Supabase.Postgrest.Models;

namespace SubashaVentures.Services.Supabase;

public interface ISupabaseService
{
    // CRUD Operations - T must inherit from BaseModel for Supabase
    Task<T?> GetByIdAsync<T>(string table, string id) where T : BaseModel, new();
    Task<List<T>> GetAllAsync<T>(string table) where T : BaseModel, new();
    Task<List<T>> QueryAsync<T>(string table, string filter) where T : BaseModel, new();
    Task<string?> InsertAsync<T>(string table, T data) where T : BaseModel, new();
    Task<bool> UpdateAsync<T>(string table, string id, T data) where T : BaseModel, new();
    Task<bool> DeleteAsync(string table, string id);
    
    // Batch operations
    Task<bool> InsertBatchAsync<T>(string table, List<T> items) where T : BaseModel, new();
    Task<bool> UpdateBatchAsync<T>(string table, Dictionary<string, T> updates) where T : BaseModel, new();
    
    // Raw SQL execution
    Task<T?> ExecuteScalarAsync<T>(string sql);
    Task ExecuteAsync(string sql);
}
