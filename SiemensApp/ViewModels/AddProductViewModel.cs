using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SiemensApp.Models.Ui;
using SiemensApp.Mvvm;
using SiemensApp.Services;

namespace SiemensApp.ViewModels;

/// <summary>
/// ViewModel لإضافة المنتجات إلى المخزن الداخلي.
/// </summary>
public sealed partial class AddProductViewModel : ViewModelBase
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly IInvoiceSchemaInitializer _schema;
    private readonly IDialogService _dialog;
    private readonly ILogger<AddProductViewModel> _logger;

    public AddProductViewModel(
        ISqliteConnectionFactory factory,
        IInvoiceSchemaInitializer schema,
        IDialogService dialog,
        ILogger<AddProductViewModel> logger)
    {
        _factory = factory;
        _schema = schema;
        _dialog = dialog;
        _logger = logger;
    }

    public ObservableCollection<InternalItem> Items { get; } = [];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _newBarcode = string.Empty;
    [ObservableProperty] private string _newBrandName = string.Empty;
    [ObservableProperty] private string _newProductName = string.Empty;
    [ObservableProperty] private double _newPrice;
    [ObservableProperty] private int _newQuantity;
    [ObservableProperty] private string _newCurrency = "دينار عراقي";

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
                SELECT Barcode, BrandName, ProductName, Price, Quantity, Currency
                FROM InternalStock
                WHERE ProductName LIKE @p OR BrandName LIKE @p OR Barcode LIKE @p
                """;
            cmd.Parameters.AddWithValue("@p", "%" + (filter ?? string.Empty) + "%");

            Items.Clear();
            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(true);
            while (await reader.ReadAsync().ConfigureAwait(true))
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل تحميل المخزن الداخلي.");
        }
    }

    [RelayCommand]
    public async Task AddOrUpdateAsync()
    {
        if (string.IsNullOrWhiteSpace(NewBarcode) || string.IsNullOrWhiteSpace(NewProductName))
        {
            _dialog.ShowWarning("يرجى إدخال الباركود واسم المنتج.");
            return;
        }

        try
        {
            await using var connection = await _factory.CreateOpenAsync().ConfigureAwait(true);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO InternalStock(Barcode, BrandName, ProductName, Price, Quantity, Currency)
                VALUES(@b, @brand, @name, @price, @qty, @cur)
                ON CONFLICT(Barcode) DO UPDATE SET
                    BrandName=excluded.BrandName,
                    ProductName=excluded.ProductName,
                    Price=excluded.Price,
                    Quantity=excluded.Quantity,
                    Currency=excluded.Currency;
                """;
            cmd.Parameters.AddWithValue("@b", NewBarcode);
            cmd.Parameters.AddWithValue("@brand", NewBrandName);
            cmd.Parameters.AddWithValue("@name", NewProductName);
            cmd.Parameters.AddWithValue("@price", NewPrice);
            cmd.Parameters.AddWithValue("@qty", NewQuantity);
            cmd.Parameters.AddWithValue("@cur", NewCurrency);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(true);
            _logger.LogInformation("تم حفظ المنتج {Name} ({Barcode}).", NewProductName, NewBarcode);

            ClearForm();
            await LoadAsync(SearchText).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل حفظ المنتج.");
            _dialog.ShowError($"تعذر حفظ المنتج: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task DeleteAsync(string barcode)
    {
        if (string.IsNullOrEmpty(barcode))
            return;

        if (!_dialog.Confirm($"هل تريد حذف المنتج ذو الباركود {barcode}؟"))
            return;

        try
        {
            await using var connection = await _factory.CreateOpenAsync().ConfigureAwait(true);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM InternalStock WHERE Barcode=@b";
            cmd.Parameters.AddWithValue("@b", barcode);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(true);

            _logger.LogInformation("تم حذف المنتج {Barcode}.", barcode);
            await LoadAsync(SearchText).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل حذف المنتج {Barcode}.", barcode);
            _dialog.ShowError($"تعذر حذف المنتج: {ex.Message}");
        }
    }

    private void ClearForm()
    {
        NewBarcode = string.Empty;
        NewBrandName = string.Empty;
        NewProductName = string.Empty;
        NewPrice = 0;
        NewQuantity = 0;
        NewCurrency = "دينار عراقي";
    }
}
