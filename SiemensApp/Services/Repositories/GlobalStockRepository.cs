using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SiemensApp.Models;

namespace SiemensApp.Services.Repositories;

/// <summary>تنفيذ ADO.NET لـ <see cref="IGlobalStockRepository"/>.</summary>
public sealed class GlobalStockRepository : IGlobalStockRepository
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly ILogger<GlobalStockRepository> _logger;

    public GlobalStockRepository(
        ISqliteConnectionFactory factory,
        ILogger<GlobalStockRepository> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GlobalStockItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = await _factory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, ProductName, DefaultPrice, Currency, Category FROM GlobalStock;";

        var results = new List<GlobalStockItem>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(MapRow(reader));
        }
        return results;
    }

    public async Task<GlobalStockItem?> GetByNameAsync(string productName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(productName))
            return null;

        await using var conn = await _factory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, ProductName, DefaultPrice, Currency, Category FROM GlobalStock WHERE ProductName = $name LIMIT 1;";
        cmd.Parameters.AddWithValue("$name", productName);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return MapRow(reader);

        return null;
    }

    public async Task<long> AddAsync(GlobalStockItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        await using var conn = await _factory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO GlobalStock (ProductName, DefaultPrice, Currency, Category)
            VALUES ($name, $price, $currency, $category);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$name", item.ProductName);
        cmd.Parameters.AddWithValue("$price", (double)item.DefaultPrice);
        cmd.Parameters.AddWithValue("$currency", item.Currency);
        cmd.Parameters.AddWithValue("$category", item.Category);

        var newId = (long)(await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L);
        item.Id = newId;
        _logger.LogDebug("GlobalStock added: {Id} {Name}", newId, item.ProductName);
        return newId;
    }

    public async Task UpdateAsync(GlobalStockItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        await using var conn = await _factory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE GlobalStock
            SET ProductName = $name,
                DefaultPrice = $price,
                Currency = $currency,
                Category = $category
            WHERE Id = $id;
            """;
        cmd.Parameters.AddWithValue("$id", item.Id);
        cmd.Parameters.AddWithValue("$name", item.ProductName);
        cmd.Parameters.AddWithValue("$price", (double)item.DefaultPrice);
        cmd.Parameters.AddWithValue("$currency", item.Currency);
        cmd.Parameters.AddWithValue("$category", item.Category);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("GlobalStock updated: {Id}", item.Id);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var conn = await _factory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM GlobalStock WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("GlobalStock deleted: {Id}", id);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = await _factory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM GlobalStock;";

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result);
    }

    private static GlobalStockItem MapRow(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        ProductName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
        DefaultPrice = reader.IsDBNull(2) ? 0m : (decimal)reader.GetDouble(2),
        Currency = reader.IsDBNull(3) ? "دينار عراقي" : reader.GetString(3),
        Category = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
    };
}
