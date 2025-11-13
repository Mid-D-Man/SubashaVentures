using Supabase;
using Supabase.Postgrest;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Responses;
using Client = Supabase.Client;
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

    public async Task<T?> GetByIdAsync<T>(string table, string id) where T : BaseModel, new()
    {
        try
        {
            var response = await _client
                .From<T>()
                .Filter("id", Postgrest.Constants.Operator.Equals, id)
                .Single();

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting record from {Table}: {Id}", table, id);
            return null;
        }
    }

    public async Task<List<T>> GetAllAsync<T>(string table) where T : BaseModel, new()
    {
        try
        {
            var response = await _client
                .From<T>()
                .Get();

            return response.Models ?? new List<T>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all records from {Table}", table);
            return new List<T>();
        }
    }

    public async Task<List<T>> QueryAsync<T>(string table, string filter) where T : BaseModel, new()
    {
        try
        {
            var query = _client.From<T>();
            
            // Apply basic filters
            if (filter.Contains("ORDER BY"))
            {
                var parts = filter.Split(new[] { "ORDER BY" }, StringSplitOptions.None);
                
                if (parts.Length > 1)
                {
                    var orderPart = parts[1].Trim();
                    if (orderPart.Contains("DESC"))
                    {
                        var column = orderPart.Replace("DESC", "").Trim();
                        query = query.Order(column, Postgrest.Constants.Ordering.Descending);
                    }
                    else if (orderPart.Contains("ASC"))
                    {
                        var column = orderPart.Replace("ASC", "").Trim();
                        query = query.Order(column, Postgrest.Constants.Ordering.Ascending);
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

    public async Task<string?> InsertAsync<T>(string table, T data) where T : BaseModel, new()
    {
        try
        {
            var response = await _client
                .From<T>()
                .Insert(data);

            var inserted = response.Models?.FirstOrDefault();
            
            // Use reflection to get the Id property value
            if (inserted != null)
            {
                var idProperty = typeof(T).GetProperty("Id");
                if (idProperty != null)
                {
                    var idValue = idProperty.GetValue(inserted);
                    return idValue?.ToString();
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting into {Table}", table);
            return null;
        }
    }

    public async Task<bool> UpdateAsync<T>(string table, string id, T data) where T : BaseModel, new()
    {
        try
        {
            await _client
                .From<T>()
                .Filter("id", Postgrest.Constants.Operator.Equals, id)
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
            // Use RPC or raw query for delete operations
            await _client.Rpc("delete_by_id", new Dictionary<string, object>
            {
                { "table_name", table },
                { "record_id", id }
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting from {Table}: {Id}", table, id);
            return false;
        }
    }

    public async Task<bool> InsertBatchAsync<T>(string table, List<T> items) where T : BaseModel, new()
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

    public async Task<bool> UpdateBatchAsync<T>(string table, Dictionary<string, T> updates) where T : BaseModel, new()
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
            // Use RPC for custom SQL
            var result = await _client.Rpc("execute_scalar", new Dictionary<string, object>
            {
                { "query", sql }
            });

            if (result != null && typeof(T).IsAssignableFrom(result.GetType()))
            {
                return (T)result;
            }

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
            await _client.Rpc("execute_query", new Dictionary<string, object>
            {
                { "query", sql }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SQL");
        }
    }
}
