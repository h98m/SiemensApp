using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SiemensApp.Models.Ui;
using SiemensApp.Mvvm;
using SiemensApp.Services;

namespace SiemensApp.ViewModels;

/// <summary>ViewModel لعرض المخزن الخارجي (GlobalStock).</summary>
public sealed partial class StorageViewModel : ViewModelBase
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly IInvoiceSchemaInitializer _schema;
    private readonly ILogger<StorageViewModel> _logger;

    public StorageViewModel(
        ISqliteConnectionFactory factory,
        IInvoiceSchemaInitializer schema,
        ILogger<StorageViewModel> logger)
    {
        _factory = factory;
        _schema = schema;
        _logger = logger;
    }

    public ObservableCollection<StorageItem> Items { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value) => _ = LoadAsync(value);

    [RelayCommand]
    public async Task LoadAsync(string filter = "")
    {
        try
        {
            await _schema.EnsureCreatedAsync().ConfigureAwait(true);

            await using var connection = await _factory.CreateOpenAsync().ConfigureAwait(true);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT ProductName, DefaultPrice, Currency
                FROM GlobalStock
                WHERE ProductName LIKE @p
                """;
            cmd.Parameters.AddWithValue("@p", "%" + (filter ?? string.Empty) + "%");

            Items.Clear();
            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(true);
            while (await reader.ReadAsync().ConfigureAwait(true))
            {
                string currency = reader.IsDBNull(2) ? "دينار عراقي" : reader.GetString(2);
                string symbol = currency == "دولار أمريكي" ? " $" : " د.ع";
                double price = reader.IsDBNull(1) ? 0 : reader.GetDouble(1);

                Items.Add(new StorageItem
                {
                    ProductName = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    DefaultPrice = price,
                    Currency = currency,
                    DisplayPrice = price.ToString("N0") + symbol
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل تحميل المخزن الخارجي.");
        }
    }
}
