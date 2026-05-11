using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SiemensApp.Configuration;
using SiemensApp.Data;
using SiemensApp.Models;
using SiemensApp.Services;
using SiemensApp.Services.Repositories;

namespace SiemensApp.Tests;

/// <summary>
/// اختبارات تكاملية للمستودعات (Repositories) باستخدام SQLite في الذاكرة.
/// تتحقق من CRUD الأساسي للمنتجات، الديون، الفواتير، المخزن العام والداخلي.
/// </summary>
public sealed class RepositoryTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteConnection _sharedConnection;
    private readonly DbContextOptions<AppDbContext> _efOptions;
    private readonly TestSqliteConnectionFactory _factory;
    private readonly TestDbContextFactory _ctxFactory;

    public RepositoryTests()
    {
        // قاعدة SQLite في الذاكرة باسم فريد لكل instance من الـ test — بحيث تشترك كل
        // الاتصالات على نفس البيانات. اتصال مشترك يبقى مفتوحاً للحفاظ على حياة القاعدة.
        var sharedDbName = $"repo-tests-{Guid.NewGuid():N}";
        var connectionString = $"Data Source=file:{sharedDbName}?mode=memory&cache=shared";

        _sharedConnection = new SqliteConnection(connectionString);
        _sharedConnection.Open();

        _efOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString)
            .Options;

        _factory = new TestSqliteConnectionFactory(connectionString);
        _ctxFactory = new TestDbContextFactory(_efOptions);
    }

    public async Task InitializeAsync()
    {
        // إنشاء جداول EF
        await using var ctx = new AppDbContext(_efOptions);
        await ctx.Database.EnsureCreatedAsync();

        // إنشاء جداول الـ raw SQL
        await using var cmd = _sharedConnection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS GlobalStock (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductName TEXT UNIQUE,
                DefaultPrice REAL,
                Currency TEXT DEFAULT 'دينار عراقي',
                Category TEXT
            );
            CREATE TABLE IF NOT EXISTS InternalStock (
                Barcode TEXT PRIMARY KEY,
                BrandName TEXT,
                ProductName TEXT,
                Price REAL,
                Quantity INTEGER,
                Currency TEXT DEFAULT 'دينار عراقي'
            );
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _sharedConnection.Close();
        _sharedConnection.Dispose();
    }

    // -------- ProductRepository --------

    [Fact]
    public async Task ProductRepository_AddGetUpdateDelete_WorksEndToEnd()
    {
        var repo = new ProductRepository(_ctxFactory, NullLogger<ProductRepository>.Instance);

        await repo.AddAsync(new Product { Barcode = "111", Name = "Test", Price = 5m, Quantity = 10 });

        var byBarcode = await repo.GetByBarcodeAsync("111");
        Assert.NotNull(byBarcode);
        Assert.Equal("Test", byBarcode!.Name);

        byBarcode.Price = 7.5m;
        await repo.UpdateAsync(byBarcode);

        var updated = await repo.GetByBarcodeAsync("111");
        Assert.Equal(7.5m, updated!.Price);
        Assert.Equal(1, await repo.CountAsync());

        await repo.DeleteAsync("111");
        Assert.Null(await repo.GetByBarcodeAsync("111"));
        Assert.Equal(0, await repo.CountAsync());
    }

    [Fact]
    public async Task ProductRepository_GetByBarcode_WithEmptyBarcode_ReturnsNull()
    {
        var repo = new ProductRepository(_ctxFactory, NullLogger<ProductRepository>.Instance);
        Assert.Null(await repo.GetByBarcodeAsync(""));
    }

    // -------- DebtRepository --------

    [Fact]
    public async Task DebtRepository_AddAndGetTotal_ReturnsCorrectAmounts()
    {
        var repo = new DebtRepository(_ctxFactory, NullLogger<DebtRepository>.Instance);

        await repo.AddAsync(new Debt { Name = "Alice", Amount = 100m, Currency = "IQD" });
        await repo.AddAsync(new Debt { Name = "Bob", Amount = 200m, Currency = "IQD" });
        await repo.AddAsync(new Debt { Name = "Carol", Amount = 50m, Currency = "USD" });

        Assert.Equal(300m, await repo.GetTotalByCurrencyAsync("IQD"));
        Assert.Equal(50m, await repo.GetTotalByCurrencyAsync("USD"));
        Assert.Equal(0m, await repo.GetTotalByCurrencyAsync("EUR"));

        var all = await repo.GetAllAsync();
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task DebtRepository_UpdateAndDelete_PersistsChanges()
    {
        var repo = new DebtRepository(_ctxFactory, NullLogger<DebtRepository>.Instance);

        await repo.AddAsync(new Debt { Name = "Dave", Amount = 75m, Currency = "USD" });

        var dave = await repo.GetByNameAsync("Dave");
        Assert.NotNull(dave);
        dave!.Amount = 80m;
        await repo.UpdateAsync(dave);

        Assert.Equal(80m, (await repo.GetByNameAsync("Dave"))!.Amount);

        await repo.DeleteAsync("Dave");
        Assert.Null(await repo.GetByNameAsync("Dave"));
    }

    // -------- SaleRecordRepository --------

    [Fact]
    public async Task SaleRecordRepository_AddAndPaginate_ReturnsRecentFirst()
    {
        var repo = new SaleRecordRepository(_ctxFactory, NullLogger<SaleRecordRepository>.Instance);

        for (int i = 0; i < 5; i++)
        {
            await repo.AddAsync(new SaleRecord
            {
                CustomerName = $"Cust{i}",
                Date = DateTime.UtcNow.AddDays(-i),
                Items = [new SaleItem { Name = "X", Qty = 1, UnitPrice = i + 1m }]
            });
        }

        Assert.Equal(5, await repo.CountAsync());

        // أول صفحة (2 عناصر) ⇒ الأحدث أولاً = Cust0, Cust1
        var page = await repo.GetPageAsync(0, 2);
        Assert.Equal(2, page.Count);
        Assert.Equal("Cust0", page[0].CustomerName);
        Assert.Equal("Cust1", page[1].CustomerName);

        // الصفحة الثانية ⇒ Cust2, Cust3
        var page2 = await repo.GetPageAsync(1, 2);
        Assert.Equal(2, page2.Count);
        Assert.Equal("Cust2", page2[0].CustomerName);
    }

    // -------- GlobalStockRepository --------

    [Fact]
    public async Task GlobalStockRepository_FullLifecycle_WorksCorrectly()
    {
        var repo = new GlobalStockRepository(_factory, NullLogger<GlobalStockRepository>.Instance);

        var id = await repo.AddAsync(new GlobalStockItem
        {
            ProductName = "Widget",
            DefaultPrice = 12.5m,
            Category = "Tools"
        });
        Assert.True(id > 0);

        var fetched = await repo.GetByNameAsync("Widget");
        Assert.NotNull(fetched);
        Assert.Equal(12.5m, fetched!.DefaultPrice);

        fetched.DefaultPrice = 15m;
        await repo.UpdateAsync(fetched);

        Assert.Equal(15m, (await repo.GetByNameAsync("Widget"))!.DefaultPrice);
        Assert.Equal(1, await repo.CountAsync());

        await repo.DeleteAsync(id);
        Assert.Equal(0, await repo.CountAsync());
    }

    // -------- InternalStockRepository --------

    [Fact]
    public async Task InternalStockRepository_UpsertTwice_UpdatesExistingRecord()
    {
        var repo = new InternalStockRepository(_factory, NullLogger<InternalStockRepository>.Instance);

        await repo.UpsertAsync(new InternalStockItem
        {
            Barcode = "ABC123",
            BrandName = "Acme",
            ProductName = "Gadget",
            Price = 10m,
            Quantity = 5
        });

        // Upsert ثاني بنفس الباركود يجب أن يحدّث، لا يكرّر
        await repo.UpsertAsync(new InternalStockItem
        {
            Barcode = "ABC123",
            BrandName = "Acme",
            ProductName = "Gadget v2",
            Price = 12m,
            Quantity = 8
        });

        Assert.Equal(1, await repo.CountAsync());

        var item = await repo.GetByBarcodeAsync("ABC123");
        Assert.NotNull(item);
        Assert.Equal("Gadget v2", item!.ProductName);
        Assert.Equal(12m, item.Price);
        Assert.Equal(8, item.Quantity);

        await repo.DeleteAsync("ABC123");
        Assert.Equal(0, await repo.CountAsync());
    }

    // -------- Helpers --------

    private sealed class TestSqliteConnectionFactory(string connectionString) : ISqliteConnectionFactory
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

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }
}
