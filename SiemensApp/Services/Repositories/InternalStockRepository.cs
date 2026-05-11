using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SiemensApp.Models;

namespace SiemensApp.Services.Repositories;

/// <summary>تنفيذ ADO.NET لـ <see cref="IInternalStockRepository"/>.</summary>
public sealed class InternalStockRepository : IInternalStockRepository
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly ILogger<InternalStockRepository> _logger;

    public InternalStockRepository(
        ISqliteConnectionFactory factory,
        ILogger<InternalStockRepository> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<InternalStockItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = await _factory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Barcode, BrandName, ProductName, Price, Quantity, Currency FROM InternalStock;";

        var results = new List<InternalStockItem>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(MapRow(reader));
        }
        return results;
    }

    public async Task<InternalStockItem?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(barcode))
            return null;

        await using var conn = await _factory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Barcode, BrandName, ProductName, Price, Quantity, Currency FROM InternalStock WHERE Barcode = $bc LIMIT 1;";
        cmd.Parameters.AddWithValue("$bc", barcode);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return MapRow(reader);

        return null;
    }

    public async Task UpsertAsync(InternalStockItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        await using var conn = await _factory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO InternalStock (Barcode, BrandName, ProductName, Price, Quantity, Currency)
            VALUES ($bc, $brand, $name, $price, $qty, $currency)
            ON CONFLICT(Barcode) DO UPDATE SET
                BrandName = excluded.BrandName,
                ProductName = excluded.ProductName,
                Price = excluded.Price,
                Quantity = excluded.Quantity,
                Currency = excluded.Currency;
            """;
        cmd.Parameters.AddWithValue("$bc", item.Barcode);
        cmd.Parameters.AddWithValue("$brand", item.BrandName);
        cmd.Parameters.AddWithValue("$name", item.ProductName);
        cmd.Parameters.AddWithValue("$price", (double)item.Price);
        cmd.Parameters.AddWithValue("$qty", item.Quantity);
        cmd.Parameters.AddWithValue("$currency", item.Currency);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("InternalStock upserted: {Barcode}", item.Barcode);
    }

    public async Task DeleteAsync(string barcode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(barcode))
            return;

        await using var conn = await _factory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM InternalStock WHERE Barcode = $bc;";
        cmd.Parameters.AddWithValue("$bc", barcode);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("InternalStock deleted: {Barcode}", barcode);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = await _factory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM InternalStock;";

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result);
    }

    private static InternalStockItem MapRow(SqliteDataReader reader) => new()
    {
        Barcode = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
        BrandName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
        ProductName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
        Price = reader.IsDBNull(3) ? 0m : (decimal)reader.GetDouble(3),
        Quantity = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
        Currency = reader.IsDBNull(5) ? "دينار عراقي" : reader.GetString(5),
    };
}
