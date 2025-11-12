using Supabase;
using SubashaVentures.Utilities.HelperScripts;
using System.Text.Json;

namespace SubashaVentures.Services.Supabase;

public class SupabaseService : ISupabaseService
{
    private readonly Client _client;
    private readonly ILogger<SupabaseService> _logger;

    public SupabaseService(Client client, ILogger<SupabaseService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<T?> GetByIdAsync<T>(string table, string id) where T : class, new()
    {
        try
        {
            var response = await _client
                .From<T>()
                .Select("*")
                .Where(x => GetId(x) == id)
                .Single();

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting record from {Table}: {Id}", table, id);
            return null;
        }
    }

    public async Task<List<T>> GetAllAsync<T>(string table) where T : class, new()
    {
        try
        {
            var response = await _client
                .From<T>()
                .Select("*")
                .Get();

            return response.Models ?? new List<T>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all records from {Table}", table);
            return new List<T>();
        }
    }

    public async Task<List<T>> QueryAsync<T>(string table, string filter) where T : class, new()
    {
        try
        {
            // For simple filters like "is_active = true AND is_deleted = false"
            // We need to parse and apply them manually since Supabase C# client
            // doesn't support raw SQL filters directly
            
            var query = _client.From<T>().Select("*");
            
            // Apply basic filters (this is simplified - expand as needed)
            if (filter.Contains("ORDER BY"))
            {
                var parts = filter.Split(new[] { "ORDER BY" }, StringSplitOptions.None);
                // Apply the where conditions from parts[0] if any
                // Apply ordering from parts[1]
                
                if (parts.Length > 1)
                {
                    var orderPart = parts[1].Trim();
                    if (orderPart.Contains("DESC"))
                    {
                        var column = orderPart.Replace("DESC", "").Trim();
                        query = query.Order(column, Supabase.Postgrest.Constants.Ordering.Descending);
                    }
                    else if (orderPart.Contains("ASC"))
                    {
                        var column = orderPart.Replace("ASC", "").Trim();
                        query = query.Order(column, Supabase.Postgrest.Constants.Ordering.Ascending);
                    }
                }
            }
            
            var response = await query.Get();
            return response.Models ?? new List<T>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying {Table} with filter: {Filter}", table, filter);
            return new List<T>();
        }
    }

    public async Task<string?> InsertAsync<T>(string table, T data) where T : class, new()
    {
        try
        {
            var response = await _client
                .From<T>()
                .Insert(data);

            var inserted = response.Models?.FirstOrDefault();
            return inserted != null ? GetId(inserted) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting into {Table}", table);
            return null;
        }
    }

    public async Task<bool> UpdateAsync<T>(string table, string id, T data) where T : class, new()
    {
        try
        {
            await _client
                .From<T>()
                .Where(x => GetId(x) == id)
                .Update(data);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating {Table}: {Id}", table, id);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(string table, string id)
    {
        try
        {
            await _client
                .From<dynamic>()
                .Where(x => x.id == id)
                .Delete();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting from {Table}: {Id}", table, id);
            return false;
        }
    }

    public async Task<bool> InsertBatchAsync<T>(string table, List<T> items) where T : class, new()
    {
        try
        {
            await _client
                .From<T>()
                .Insert(items);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch inserting into {Table}", table);
            return false;
        }
    }

    public async Task<bool> UpdateBatchAsync<T>(string table, Dictionary<string, T> updates) where T : class, new()
    {
        try
        {
            foreach (var (id, data) in updates)
            {
                await UpdateAsync(table, id, data);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch updating {Table}", table);
            return false;
        }
    }

    public async Task<T?> ExecuteScalarAsync<T>(string sql)
    {
        try
        {
            // For count queries, we can use the Count method
            if (sql.Contains("COUNT(*)"))
            {
                // Extract table name from SQL
                var tableName = ExtractTableName(sql);
                
                // This is a workaround - get all and count
                // In production, you'd want to use RPC or a proper count endpoint
                var response = await _client
                    .From<dynamic>()
                    .Select("*")
                    .Get();

                var count = response.Models?.Count ?? 0;
                return (T)(object)count;
            }

            _logger.LogWarning("ExecuteScalarAsync not fully supported for: {Sql}", sql);
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing scalar SQL");
            return default;
        }
    }

    public async Task ExecuteAsync(string sql)
    {
        try
        {
            // Raw SQL execution requires RPC functions in Supabase
            // This is a placeholder - implement based on your needs
            _logger.LogWarning("ExecuteAsync requires RPC functions: {Sql}", sql);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SQL");
        }
    }

    // Helper method to get Id from dynamic object
    private string GetId(object obj)
    {
        if (obj == null) return string.Empty;

        var type = obj.GetType();
        var idProperty = type.GetProperty("Id") ?? type.GetProperty("id");
        
        if (idProperty != null)
        {
            var value = idProperty.GetValue(obj);
            return value?.ToString() ?? string.Empty;
        }

        return string.Empty;
    }

    private string ExtractTableName(string sql)
    {
        // Simple extraction - enhance as needed
        var parts = sql.Split(new[] { "FROM", "from" }, StringSplitOptions.None);
        if (parts.Length > 1)
        {
            var tablePart = parts[1].Trim().Split(' ')[0];
            return tablePart;
        }
        return string.Empty;
    }
}
