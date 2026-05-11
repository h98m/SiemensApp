using Microsoft.Extensions.Logging;

namespace SiemensApp.Services;

/// <summary>
/// يضمن وجود الجداول/الأعمدة الإضافية التي يستخدمها الكود غير-EF (مثل GlobalStock، InternalStock، Invoices).
/// يحلّ محل أكواد ALTER TABLE المتناثرة داخل ملفات الـ Views.
/// </summary>
public interface IInvoiceSchemaInitializer
{
    /// <summary>تهيئة الجداول الإضافية وإضافة أعمدة DollarRate / TotalAmountDollar إذا لم تكن موجودة.</summary>
    Task EnsureCreatedAsync(CancellationToken cancellationToken = default);
}

public sealed class InvoiceSchemaInitializer : IInvoiceSchemaInitializer
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly ILogger<InvoiceSchemaInitializer> _logger;

    public InvoiceSchemaInitializer(
        ISqliteConnectionFactory factory,
        ILogger<InvoiceSchemaInitializer> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _factory.CreateOpenAsync(cancellationToken).ConfigureAwait(false);

        // 1. جدول المخزن العام (GlobalStock)
        await ExecAsync(connection, """
            CREATE TABLE IF NOT EXISTS GlobalStock (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductName TEXT UNIQUE,
                DefaultPrice REAL,
                Currency TEXT DEFAULT 'دينار عراقي',
                Category TEXT
            );
            """, cancellationToken).ConfigureAwait(false);

        // 2. جدول المخزن الداخلي (InternalStock)
        await ExecAsync(connection, """
            CREATE TABLE IF NOT EXISTS InternalStock (
                Barcode TEXT PRIMARY KEY,
                BrandName TEXT,
                ProductName TEXT,
                Price REAL,
                Quantity INTEGER,
                Currency TEXT DEFAULT 'دينار عراقي'
            );
            """, cancellationToken).ConfigureAwait(false);

        // 3. أعمدة إضافية على جدول الفواتير — تُتجاهَل الأخطاء إذا كانت موجودة
        await TryAddColumnAsync(connection, "Invoices", "TotalAmountDollar", "REAL", cancellationToken).ConfigureAwait(false);
        await TryAddColumnAsync(connection, "Invoices", "DollarRate", "REAL", cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("تم التحقق من سكيمة الجداول الإضافية بنجاح.");
    }

    private static async Task ExecAsync(Microsoft.Data.Sqlite.SqliteConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task TryAddColumnAsync(
        Microsoft.Data.Sqlite.SqliteConnection conn,
        string table, string column, string sqlType,
        CancellationToken ct)
    {
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {sqlType};";
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("تمت إضافة العمود {Column} إلى {Table}.", column, table);
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // العمود موجود مسبقاً — تجاهل
        }
    }
}
