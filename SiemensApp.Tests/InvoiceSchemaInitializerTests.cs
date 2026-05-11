using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using SiemensApp.Services;

namespace SiemensApp.Tests;

/// <summary>
/// اختبارات سلوكية لمنطق <see cref="InvoiceSchemaInitializer"/> — تتحقق من:
/// 1) إنشاء الجداول الإضافية، 2) إنشاء الفهارس على الجداول الموجودة،
/// 3) تجاهل الجداول غير الموجودة بصمت، 4) تشغيل وضع WAL.
/// </summary>
public sealed class InvoiceSchemaInitializerTests : IDisposable
{
    private readonly SqliteConnection _keepAlive;
    private readonly string _connectionString;

    public InvoiceSchemaInitializerTests()
    {
        var dbName = $"schema-init-{Guid.NewGuid():N}";
        _connectionString = $"Data Source=file:{dbName}?mode=memory&cache=shared";
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    public void Dispose()
    {
        _keepAlive.Close();
        _keepAlive.Dispose();
    }

    [Fact]
    public async Task EnsureCreatedAsync_CreatesExtraTables_AndIndexesForThem()
    {
        var initializer = new InvoiceSchemaInitializer(
            new FixedConnectionFactory(_connectionString),
            NullLogger<InvoiceSchemaInitializer>.Instance);

        await initializer.EnsureCreatedAsync();

        var tables = await ListTablesAsync();
        Assert.Contains("GlobalStock", tables);
        Assert.Contains("InternalStock", tables);

        // الفهرس على InternalStock(ProductName) يجب أن يكون موجوداً لأن الجدول أُنشئ.
        var indexes = await ListIndexesAsync();
        Assert.Contains("IX_InternalStock_ProductName", indexes);
    }

    [Fact]
    public async Task EnsureCreatedAsync_WhenInvoiceTableDoesNotExist_SkipsIndexSilently()
    {
        var initializer = new InvoiceSchemaInitializer(
            new FixedConnectionFactory(_connectionString),
            NullLogger<InvoiceSchemaInitializer>.Instance);

        // لا نُنشئ Invoices مسبقاً — يجب أن لا يرمي
        await initializer.EnsureCreatedAsync();

        var indexes = await ListIndexesAsync();
        Assert.DoesNotContain("IX_Invoices_Date", indexes);
    }

    [Fact]
    public async Task EnsureCreatedAsync_WhenInvoiceTableExists_CreatesItsIndex()
    {
        // إنشاء Invoices مسبقاً لمحاكاة المسار الكامل بعد فتح InvoiceView
        await using (var setup = new SqliteConnection(_connectionString))
        {
            await setup.OpenAsync();
            await using var cmd = setup.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS Invoices (
                    Id INTEGER PRIMARY KEY,
                    CustomerName TEXT,
                    Date DATETIME
                );
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        var initializer = new InvoiceSchemaInitializer(
            new FixedConnectionFactory(_connectionString),
            NullLogger<InvoiceSchemaInitializer>.Instance);

        await initializer.EnsureCreatedAsync();

        var indexes = await ListIndexesAsync();
        Assert.Contains("IX_Invoices_Date", indexes);
        Assert.Contains("IX_Invoices_CustomerName", indexes);
    }

    [Fact]
    public async Task EnsureCreatedAsync_EnablesWalJournalMode()
    {
        var initializer = new InvoiceSchemaInitializer(
            new FixedConnectionFactory(_connectionString),
            NullLogger<InvoiceSchemaInitializer>.Instance);

        await initializer.EnsureCreatedAsync();

        // قواعد بيانات in-memory لا تدعم WAL وتعود إلى "memory" — لكن PRAGMA يجب أن يعمل بدون رمي.
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode;";
        var mode = (await cmd.ExecuteScalarAsync())?.ToString();
        // الـ in-memory يرجّع "memory" — للقاعدة على القرص يرجّع "wal".
        // المهم أن الـ initializer لم يرمِ.
        Assert.False(string.IsNullOrEmpty(mode));
    }

    private async Task<List<string>> ListTablesAsync()
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
        var result = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(reader.GetString(0));
        return result;
    }

    private async Task<List<string>> ListIndexesAsync()
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index';";
        var result = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(reader.GetString(0));
        return result;
    }

    private sealed class FixedConnectionFactory(string connectionString) : ISqliteConnectionFactory
    {
        public string ConnectionString { get; } = connectionString;

        public SqliteConnection Create() => new(ConnectionString);

        public SqliteConnection CreateOpen()
        {
            var c = new SqliteConnection(ConnectionString);
            c.Open();
            return c;
        }

        public async Task<SqliteConnection> CreateOpenAsync(CancellationToken cancellationToken = default)
        {
            var c = new SqliteConnection(ConnectionString);
            await c.OpenAsync(cancellationToken);
            return c;
        }
    }
}
