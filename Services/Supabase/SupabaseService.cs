using Supabase;
using SubashaVentures.Utilities.HelperScripts;
using Postgrest.Models;
using Postgrest.Responses;

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

    public async Task<T?> GetByIdAsync<T>(string table, string id) where T : class
    {
        try
        {
            var response = await _client
                .From<T>()
                .Where(x => ((dynamic)x).Id == id)
                .Get();

            return response.Models.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting record from {Table}: {Id}", table, id);
            return null;
        }
    }

    public async Task<List<T>> GetAllAsync<T>(string table) where T : class
    {
        try
        {
            var response = await _client.From<T>().Get();
            return response.Models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all records from {Table}", table);
            return new List<T>();
        }
    }

    public async Task<List<T>> QueryAsync<T>(string table, string filter) where T : class
    {
        try
        {
            // Use raw SQL query for complex filters
            var response = await _client
                .From<T>()
                .Filter("", Postgrest.Constants.Operator.Raw, filter)
                .Get();

            return response.Models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying {Table} with filter: {Filter}", table, filter);
            return new List<T>();
        }
    }

    public async Task<string?> InsertAsync<T>(string table, T data) where T : class
    {
        try
        {
            var response = await _client.From<T>().Insert(data);
            var inserted = response.Models.FirstOrDefault();
            return inserted != null ? ((dynamic)inserted).Id : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting into {Table}", table);
            return null;
        }
    }

    public async Task<bool> UpdateAsync<T>(string table, string id, T data) where T : class
    {
        try
        {
            await _client
                .From<T>()
                .Where(x => ((dynamic)x).Id == id)
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
                .Where(x => x.Id == id)
                .Delete();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting from {Table}: {Id}", table, id);
            return false;
        }
    }

    public async Task<bool> InsertBatchAsync<T>(string table, List<T> items) where T : class
    {
        try
        {
            await _client.From<T>().Insert(items);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch inserting into {Table}", table);
            return false;
        }
    }

    public async Task<bool> UpdateBatchAsync<T>(string table, Dictionary<string, T> updates) where T : class
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
            // This requires RPC function setup in Supabase
            var response = await _client.Rpc("execute_sql", new { query = sql });
            return JsonHelper.Deserialize<T>(response.Content);
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
            await _client.Rpc("execute_sql", new { query = sql });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SQL");
        }
    }
}
