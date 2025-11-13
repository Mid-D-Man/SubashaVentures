using Supabase;
using Supabase.Postgrest.Models;
using SubashaVentures.Utilities.HelperScripts;

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
                .Select("*")
                .Where(x => x.Id == id)
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

    public async Task<List<T>> QueryAsync<T>(string table, string filter) where T : BaseModel, new()
    {
        try
        {
            var query = _client.From<T>().Select("*");
            
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

    public async Task<string?> InsertAsync<T>(string table, T data) where T : BaseModel, new()
    {
        try
        {
            var response = await _client
                .From<T>()
                .Insert(data);

            var inserted = response.Models?.FirstOrDefault();
            return inserted?.Id;
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
                .Where(x => x.Id == id)
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
            // For delete, we need to use a generic BaseModel approach
            // This is a simplified version - you may need to specify the actual table type
            await _client.Postgrest
                .Table(table)
                .Delete(new Dictionary<string, string> { { "id", id } });

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
            if (sql.Contains("COUNT(*)"))
            {
                var tableName = ExtractTableName(sql);
                
                // This is a workaround - get all and count
                // In production, use RPC functions for better performance
                var response = await _client.Postgrest
                    .Table(tableName)
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
            _logger.LogWarning("ExecuteAsync requires RPC functions: {Sql}", sql);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SQL");
        }
    }

    private string ExtractTableName(string sql)
    {
        var parts = sql.Split(new[] { "FROM", "from" }, StringSplitOptions.None);
        if (parts.Length > 1)
        {
            var tablePart = parts[1].Trim().Split(' ')[0];
            return tablePart;
        }
        return string.Empty;
    }
}
