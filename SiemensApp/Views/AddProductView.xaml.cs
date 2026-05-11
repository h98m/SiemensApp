using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using SiemensApp.Models.Ui;
using SiemensApp.Services;

namespace SiemensApp.Views;

/// <summary>
/// شاشة إضافة/تعديل/حذف المنتجات (المخزن الداخلي).
/// تم تبسيط الـ code-behind ليُمحرّك عبر خدمات مُحقَنة (DI)، وتحويل العمليات إلى async.
/// </summary>
public partial class AddProductView : UserControl
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly IInvoiceSchemaInitializer _schema;
    private readonly IDialogService _dialog;
    private readonly ILogger<AddProductView> _logger;

    /// <summary>قائمة العرض المرتبطة بالـ DataGrid.</summary>
    public ObservableCollection<InternalItem> CurrentList { get; } = [];

    private bool _isEditMode;

    public AddProductView(
        ISqliteConnectionFactory factory,
        IInvoiceSchemaInitializer schema,
        IDialogService dialog,
        ILogger<AddProductView> logger)
    {
        _factory = factory;
        _schema = schema;
        _dialog = dialog;
        _logger = logger;

        InitializeComponent();
        Loaded += async (_, _) => await LoadDataAsync().ConfigureAwait(true);
    }

    /// <summary>تحميل قائمة المخزن الداخلي مع دعم الفلتر.</summary>
    private async Task LoadDataAsync(string filter = "")
    {
        try
        {
            await _schema.EnsureCreatedAsync().ConfigureAwait(true);

            CurrentList.Clear();
            await using var connection = await _factory.CreateOpenAsync().ConfigureAwait(true);
            await using var cmd = connection.CreateCommand();

            string query = "SELECT Barcode, BrandName, ProductName, Price, Quantity, Currency FROM InternalStock";
            if (!string.IsNullOrEmpty(filter))
            {
                query += " WHERE ProductName LIKE @p OR BrandName LIKE @p OR Barcode LIKE @p";
                cmd.Parameters.AddWithValue("@p", "%" + filter + "%");
            }
            query += " ORDER BY Barcode DESC";
            cmd.CommandText = query;

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(true);
            while (await reader.ReadAsync().ConfigureAwait(true))
            {
                double price = reader.IsDBNull(3) ? 0 : reader.GetDouble(3);
                string curr = reader.IsDBNull(5) ? "IQD" : reader.GetString(5);
                string currSymbol = curr == "USD" ? "$" : "د.ع";

                CurrentList.Add(new InternalItem
                {
                    Barcode = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    BrandName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    ProductName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Price = price,
                    Quantity = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    Currency = curr,
                    DisplayPrice = price.ToString("N0", CultureInfo.InvariantCulture) + " " + currSymbol
                });
            }

            dgvAddedItems.ItemsSource = null;
            dgvAddedItems.ItemsSource = CurrentList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل تحميل المخزن الداخلي.");
            _dialog.ShowError($"خطأ بالتحميل: {ex.Message}");
        }
    }

    private async void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        => await LoadDataAsync(txtSearch.Text).ConfigureAwait(true);

    private void ShowAddCard_Click(object sender, RoutedEventArgs e)
    {
        _isEditMode = false;
        if (btnSaveAction is not null)
            btnSaveAction.Content = "إضافة مادة";
        txtBarcode.IsEnabled = true;
        ClearInputs();
        AddProductCard.Visibility = Visibility.Visible;
    }

    private void HideAddCard_Click(object sender, RoutedEventArgs e)
        => AddProductCard.Visibility = Visibility.Collapsed;

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(txtBarcode.Text))
            return;

        try
        {
            string selectedCurrency = rbUSD.IsChecked == true ? "USD" : "IQD";

            await using var connection = await _factory.CreateOpenAsync().ConfigureAwait(true);
            await using var cmd = connection.CreateCommand();

            if (_isEditMode)
            {
                cmd.CommandText = """
                    UPDATE InternalStock
                    SET BrandName=@br, ProductName=@p, Price=@pr, Quantity=@q, Currency=@curr
                    WHERE Barcode=@b
                    """;
            }
            else
            {
                // فحص يدوي لتجنّب خطأ الـ Conflict إذا لم يكن الباركود مفتاحاً أساسياً
                cmd.CommandText = "SELECT COUNT(*) FROM InternalStock WHERE Barcode = @b";
                cmd.Parameters.AddWithValue("@b", txtBarcode.Text);
                long count = (long)(await cmd.ExecuteScalarAsync().ConfigureAwait(true) ?? 0L);
                cmd.Parameters.Clear();

                cmd.CommandText = count > 0
                    ? """
                      UPDATE InternalStock SET
                          Quantity = Quantity + @q,
                          Price=@pr,
                          Currency=@curr
                      WHERE Barcode=@b
                      """
                    : """
                      INSERT INTO InternalStock (Barcode, BrandName, ProductName, Price, Quantity, Currency)
                      VALUES (@b, @br, @p, @pr, @q, @curr)
                      """;
            }

            cmd.Parameters.AddWithValue("@b", txtBarcode.Text);
            cmd.Parameters.AddWithValue("@br", txtBrand.Text);
            cmd.Parameters.AddWithValue("@p", txtProductName.Text);
            cmd.Parameters.AddWithValue("@pr", double.TryParse(txtPrice.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double p) ? p : 0);
            cmd.Parameters.AddWithValue("@q", int.TryParse(txtQuantity.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out int q) ? q : 0);
            cmd.Parameters.AddWithValue("@curr", selectedCurrency);

            await cmd.ExecuteNonQueryAsync().ConfigureAwait(true);

            _logger.LogInformation("تم حفظ المنتج {Name} ({Barcode}). EditMode={EditMode}",
                txtProductName.Text, txtBarcode.Text, _isEditMode);

            AddProductCard.Visibility = Visibility.Collapsed;
            await LoadDataAsync().ConfigureAwait(true);
            ClearInputs();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل حفظ المنتج {Barcode}.", txtBarcode.Text);
            _dialog.ShowError($"خطأ بالحفظ: {ex.Message}");
        }
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not InternalItem item)
            return;

        _isEditMode = true;
        txtBarcode.Text = item.Barcode;
        txtBarcode.IsEnabled = false;
        txtBrand.Text = item.BrandName;
        txtProductName.Text = item.ProductName;
        txtPrice.Text = item.Price.ToString(CultureInfo.InvariantCulture);
        txtQuantity.Text = item.Quantity.ToString(CultureInfo.InvariantCulture);

        if (item.Currency == "USD")
            rbUSD.IsChecked = true;
        else
            rbIQD.IsChecked = true;

        if (btnSaveAction is not null)
            btnSaveAction.Content = "تحديث البيانات";
        AddProductCard.Visibility = Visibility.Visible;
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not InternalItem item)
            return;

        if (!_dialog.Confirm($"حجي تريد تحذف ({item.ProductName})؟"))
            return;

        try
        {
            await using var connection = await _factory.CreateOpenAsync().ConfigureAwait(true);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM InternalStock WHERE Barcode = @b";
            cmd.Parameters.AddWithValue("@b", item.Barcode);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(true);

            _logger.LogInformation("تم حذف المنتج {Barcode}.", item.Barcode);
            await LoadDataAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل حذف المنتج {Barcode}.", item.Barcode);
            _dialog.ShowError($"تعذر الحذف: {ex.Message}");
        }
    }

    private void ClearInputs()
    {
        txtBarcode.Clear();
        txtBrand.Clear();
        txtProductName.Clear();
        txtPrice.Clear();
        txtQuantity.Clear();
        rbIQD.IsChecked = true;
    }
}
