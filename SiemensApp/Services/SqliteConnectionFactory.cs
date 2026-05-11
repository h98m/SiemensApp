using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SiemensApp.Configuration;

namespace SiemensApp.Services;

/// <summary>
/// مصنع اتصالات SQLite يحصل على سلسلة الاتصال من <see cref="DatabaseOptions"/>.
/// يحلّ محل <c>new SqliteConnection("Data Source=...")</c> المتناثر في الكود القديم،
/// ويوحّد مكان واحد للاتصال ومسار قاعدة البيانات.
/// </summary>
public interface ISqliteConnectionFactory
{
    /// <summary>إنشاء اتصال SQLite جديد (لم يُفتح بعد).</summary>
    SqliteConnection Create();

    /// <summary>إنشاء اتصال مفتوح (يستدعي Open ضمنياً).</summary>
    SqliteConnection CreateOpen();

    /// <summary>إنشاء اتصال مفتوح بشكل متزامن مع await.</summary>
    Task<SqliteConnection> CreateOpenAsync(CancellationToken cancellationToken = default);

    /// <summary>سلسلة الاتصال الحالية (للقراءة فقط).</summary>
    string ConnectionString { get; }
}

public sealed class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private readonly DatabaseOptions _options;

    public SqliteConnectionFactory(IOptions<DatabaseOptions> options)
    {
        _options = options.Value;
    }

    public string ConnectionString => _options.GetConnectionString();

    public SqliteConnection Create() => new(ConnectionString);

    public SqliteConnection CreateOpen()
    {
        var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    public async Task<SqliteConnection> CreateOpenAsync(CancellationToken cancellationToken = default)
    {
        var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        return conn;
    }
}
