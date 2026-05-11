using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SiemensApp.Models.Ui;
using SiemensApp.Mvvm;
using SiemensApp.Services;

namespace SiemensApp.ViewModels;

/// <summary>
/// ViewModel لشاشة عرض المخزن الداخلي.
/// يتعامل مع جدول <c>InternalStock</c> ويعرض القائمة كاملة مع دعم البحث.
/// </summary>
public sealed partial class InternalStorageViewModel : ViewModelBase
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly IInvoiceSchemaInitializer _schema;
    private readonly ILogger<InternalStorageViewModel> _logger;

    public InternalStorageViewModel(
        ISqliteConnectionFactory factory,
        IInvoiceSchemaInitializer schema,
        ILogger<InternalStorageViewModel> logger)
    {
        _factory = factory;
        _schema = schema;
        _logger = logger;
    }

    /// <summary>قائمة المواد المعروضة (مع تنسيق العملة).</summary>
    public ObservableCollection<InternalItem> Items { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>تحديث البحث يستدعي إعادة تحميل القائمة تلقائياً.</summary>
    partial void OnSearchTextChanged(string value) => _ = LoadAsync(value);

    [RelayCommand]
    public Task LoadAsync(string filter = "") => LoadInternalDataAsync(filter);

    private async Task LoadInternalDataAsync(string filter, CancellationToken ct = default)
    {
        try
        {
            await _schema.EnsureCreatedAsync(ct).ConfigureAwait(true);

            await using var connection = await _factory.CreateOpenAsync(ct).ConfigureAwait(true);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT Barcode, BrandName, ProductName, Price, Quantity, Currency
                FROM InternalStock
                WHERE ProductName LIKE @p OR BrandName LIKE @p OR Barcode LIKE @p
                """;
            cmd.Parameters.AddWithValue("@p", "%" + (filter ?? string.Empty) + "%");

            Items.Clear();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(true);
            while (await reader.ReadAsync(ct).ConfigureAwait(true))
            {
                string currency = reader.IsDBNull(5) ? "دينار عراقي" : reader.GetString(5);
                string symbol = currency == "دولار أمريكي" ? " $" : " د.ع";
                double price = reader.IsDBNull(3) ? 0 : reader.GetDouble(3);

                Items.Add(new InternalItem
                {
                    Barcode = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    BrandName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    ProductName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Price = price,
                    Quantity = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    Currency = currency,
                    DisplayPrice = price.ToString("N0") + symbol
                });
            }

            _logger.LogDebug("تم تحميل {Count} مادة من المخزن الداخلي.", Items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل تحميل المخزن الداخلي.");
        }
    }
}
