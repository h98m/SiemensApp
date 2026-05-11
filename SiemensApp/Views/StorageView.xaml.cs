using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using SiemensApp.Models.Ui;
using SiemensApp.Services;

namespace SiemensApp.Views;

/// <summary>
/// شاشة المخزن الخارجي (GlobalStock). تم تحويل الوصول إلى قاعدة البيانات إلى async
/// مع استخدام <see cref="ISqliteConnectionFactory"/> بدلاً من <c>new SqliteConnection</c>.
/// </summary>
public partial class StorageView : UserControl
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly IInvoiceSchemaInitializer _schema;
    private readonly IDialogService _dialog;
    private readonly ILogger<StorageView> _logger;

    public ObservableCollection<StorageItem> StockList { get; } = [];

    public StorageView(
        ISqliteConnectionFactory factory,
        IInvoiceSchemaInitializer schema,
        IDialogService dialog,
        ILogger<StorageView> logger)
    {
        _factory = factory;
        _schema = schema;
        _dialog = dialog;
        _logger = logger;

        InitializeComponent();
        Loaded += async (_, _) => await LoadStorageDataAsync().ConfigureAwait(true);
    }

    private async Task LoadStorageDataAsync(string filter = "")
    {
        try
        {
            await _schema.EnsureCreatedAsync().ConfigureAwait(true);

            StockList.Clear();
            await using var connection = await _factory.CreateOpenAsync().ConfigureAwait(true);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT ProductName, DefaultPrice, Currency
                FROM GlobalStock
                WHERE ProductName LIKE @p
                ORDER BY ProductName ASC
                """;
            cmd.Parameters.AddWithValue("@p", "%" + filter + "%");

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(true);
            while (await reader.ReadAsync().ConfigureAwait(true))
            {
                double price = reader.GetDouble(1);
                string currency = reader.IsDBNull(2) ? "دينار عراقي" : reader.GetString(2);

                string formatted = currency is "دولار أمريكي" or "$"
                    ? price.ToString("N2", CultureInfo.InvariantCulture) + " $"
                    : price.ToString("#,##0", CultureInfo.InvariantCulture) + " د.ع";

                StockList.Add(new StorageItem
                {
                    ProductName = reader.GetString(0),
                    DefaultPrice = price,
                    Currency = currency,
                    DisplayPrice = formatted
                });
            }

            dgvStorage.ItemsSource = null;
            dgvStorage.ItemsSource = StockList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل تحميل المخزن الخارجي.");
            _dialog.ShowError($"خطأ في تحميل المخزن: {ex.Message}");
        }
    }

    private void BtnEditProduct_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not StorageItem selected)
            return;

        Window editWin = new()
        {
            Width = 450,
            Height = 480,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true
        };

        var mainBorder = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(30),
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 30, Opacity = 0.15, ShadowDepth = 5 }
        };

        var mainStack = new StackPanel { FlowDirection = FlowDirection.RightToLeft };
        mainStack.Children.Add(new TextBlock
        {
            Text = "بطاقة تعديل المنتج",
            FontSize = 22, FontWeight = FontWeights.Black,
            Margin = new Thickness(0, 0, 0, 25)
        });

        // اسم المنتج
        mainStack.Children.Add(new TextBlock
        {
            Text = "اسم المنتج", FontSize = 13, FontWeight = FontWeights.Bold,
            Foreground = Brushes.Gray, Margin = new Thickness(5, 0, 0, 5)
        });
        var txtName = new TextBox
        {
            Text = selected.ProductName, Height = 45, FontSize = 15,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(12, 0, 12, 0),
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            Margin = new Thickness(0, 0, 0, 20)
        };
        mainStack.Children.Add(txtName);

        // السعر مع تنسيق آلاف
        mainStack.Children.Add(new TextBlock
        {
            Text = "السعر المعتمد", FontSize = 13, FontWeight = FontWeights.Bold,
            Foreground = Brushes.Gray, Margin = new Thickness(5, 0, 0, 5)
        });
        var txtPrice = new TextBox
        {
            Text = selected.DefaultPrice.ToString("#,##0.##", CultureInfo.InvariantCulture),
            Height = 45, FontSize = 18, FontWeight = FontWeights.Bold,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(12, 0, 12, 0),
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            Margin = new Thickness(0, 0, 0, 20)
        };
        txtPrice.TextChanged += (_, _) =>
        {
            string rawText = txtPrice.Text.Replace(",", "", StringComparison.Ordinal);
            if (decimal.TryParse(rawText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val))
            {
                int selectionStart = txtPrice.SelectionStart;
                int oldLength = txtPrice.Text.Length;
                txtPrice.Text = val.ToString("#,##0.##", CultureInfo.InvariantCulture);
                int newLength = txtPrice.Text.Length;
                txtPrice.SelectionStart = Math.Max(0, selectionStart + (newLength - oldLength));
            }
        };
        mainStack.Children.Add(txtPrice);

        // اختيار العملة
        string currentCur = selected.Currency is "$" or "دولار أمريكي" ? "$" : "د.ع";
        var btnCurrency = new Button
        {
            Height = 50, Cursor = Cursors.Hand, FontWeight = FontWeights.Bold, FontSize = 16,
            Margin = new Thickness(0, 0, 0, 25)
        };

        void UpdateBtnStyle()
        {
            if (currentCur == "$")
            {
                btnCurrency.Content = "💵 العملة: دولار أمريكي ($)";
                btnCurrency.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244));
                btnCurrency.Foreground = new SolidColorBrush(Color.FromRgb(21, 128, 61));
            }
            else
            {
                btnCurrency.Content = "🇮🇶 العملة: دينار عراقي (د.ع)";
                btnCurrency.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242));
                btnCurrency.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28));
            }
        }
        UpdateBtnStyle();
        btnCurrency.Click += (_, _) => { currentCur = currentCur == "$" ? "د.ع" : "$"; UpdateBtnStyle(); };
        mainStack.Children.Add(btnCurrency);

        // أزرار التحكم
        var gridButtons = new Grid();
        gridButtons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        gridButtons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var btnSave = new Button
        {
            Content = "حفظ التعديل", Height = 48,
            Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
            Foreground = Brushes.White, FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        var saveBorder = new Border { CornerRadius = new CornerRadius(10), Child = btnSave, ClipToBounds = true };
        Grid.SetColumn(saveBorder, 0);

        var btnCancel = new Button
        {
            Content = "إلغاء", Height = 48, Background = Brushes.Transparent,
            BorderThickness = new Thickness(0), Foreground = Brushes.DimGray, Cursor = Cursors.Hand
        };
        btnCancel.Click += (_, _) => editWin.Close();
        Grid.SetColumn(btnCancel, 1);

        btnSave.Click += async (_, _) =>
        {
            string cleanPrice = txtPrice.Text.Replace(",", "", StringComparison.Ordinal);
            if (double.TryParse(cleanPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out double newPrice))
            {
                await UpdateProductInDbAsync(selected.ProductName, txtName.Text, newPrice, currentCur).ConfigureAwait(true);
                editWin.Close();
                await LoadStorageDataAsync(txtSearchStorage.Text).ConfigureAwait(true);
            }
            else
            {
                _dialog.ShowWarning("حجي السعر لازم يكون رقم!");
            }
        };

        gridButtons.Children.Add(saveBorder);
        gridButtons.Children.Add(btnCancel);
        mainStack.Children.Add(gridButtons);

        mainBorder.Child = mainStack;
        editWin.Content = mainBorder;
        editWin.ShowDialog();
    }

    private async Task UpdateProductInDbAsync(string oldName, string newName, double price, string currency)
    {
        try
        {
            await using var connection = await _factory.CreateOpenAsync().ConfigureAwait(true);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                UPDATE GlobalStock
                SET ProductName = @newName, DefaultPrice = @price, Currency = @cur
                WHERE ProductName = @oldName
                """;
            cmd.Parameters.AddWithValue("@newName", newName);
            cmd.Parameters.AddWithValue("@price", price);
            cmd.Parameters.AddWithValue("@cur", currency);
            cmd.Parameters.AddWithValue("@oldName", oldName);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(true);

            _logger.LogInformation("تم تحديث المنتج {OldName} -> {NewName}", oldName, newName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل تحديث المنتج {OldName}.", oldName);
            _dialog.ShowError($"عذراً، صار خطأ بالتحديث: {ex.Message}");
        }
    }

    private async void BtnDeleteProduct_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not StorageItem selected)
            return;

        if (!_dialog.Confirm($"حجي متأكد تريد تحذف مادة ({selected.ProductName})؟", "تأكيد الحذف"))
            return;

        try
        {
            await using var connection = await _factory.CreateOpenAsync().ConfigureAwait(true);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM GlobalStock WHERE ProductName = @name";
            cmd.Parameters.AddWithValue("@name", selected.ProductName);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(true);

            StockList.Remove(selected);
            _logger.LogInformation("تم حذف المنتج {Name} من المخزن الخارجي.", selected.ProductName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل حذف المنتج {Name}.", selected.ProductName);
            _dialog.ShowError($"صار خطأ بالحذف: {ex.Message}");
        }
    }

    private async void txtSearchStorage_TextChanged(object sender, TextChangedEventArgs e)
        => await LoadStorageDataAsync(txtSearchStorage.Text).ConfigureAwait(true);
}
