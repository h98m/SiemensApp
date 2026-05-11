using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using SiemensApp.Models.Ui;
using SiemensApp.Services;

namespace SiemensApp.Views;

/// <summary>
/// شاشة سجل الفواتير. تستخدم خدمات DI ومصنع SQLite بدلاً من الاتصال المباشر،
/// وتُحوِّل الوصول إلى قاعدة البيانات إلى async.
/// </summary>
public partial class InvoicesListView : UserControl
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly IInvoiceSchemaInitializer _schema;
    private readonly IDialogService _dialog;
    private readonly INavigationService _navigation;
    private readonly IServiceProvider _services;
    private readonly ILogger<InvoicesListView> _logger;

    public ObservableCollection<InvoiceHeader> Invoices { get; } = [];

    public InvoicesListView(
        ISqliteConnectionFactory factory,
        IInvoiceSchemaInitializer schema,
        IDialogService dialog,
        INavigationService navigation,
        IServiceProvider services,
        ILogger<InvoicesListView> logger)
    {
        _factory = factory;
        _schema = schema;
        _dialog = dialog;
        _navigation = navigation;
        _services = services;
        _logger = logger;

        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await _schema.EnsureCreatedAsync().ConfigureAwait(true);
            await LoadInvoicesAsync().ConfigureAwait(true);
        };
    }

    private async Task LoadInvoicesAsync(string search = "")
    {
        try
        {
            Invoices.Clear();
            await using var connection = await _factory.CreateOpenAsync().ConfigureAwait(true);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT Id, InvoiceNumber, CustomerName, Phone, Date, TotalAmount,
                       InvoiceType, Currency, TotalAmountDollar, DollarRate
                FROM Invoices
                WHERE CustomerName LIKE @s
                   OR InvoiceNumber LIKE @s
                   OR Id LIKE @s
                   OR TotalAmount LIKE @s
                   OR TotalAmountDollar LIKE @s
                ORDER BY Id DESC
                """;
            cmd.Parameters.AddWithValue("@s", "%" + search + "%");

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(true);
            while (await reader.ReadAsync().ConfigureAwait(true))
            {
                decimal totalDinar = Convert.ToDecimal(reader.IsDBNull(5) ? 0 : reader.GetDouble(5));
                decimal totalDollar;

                if (!reader.IsDBNull(8))
                {
                    totalDollar = Convert.ToDecimal(reader.GetDouble(8));
                }
                else
                {
                    decimal rate = !reader.IsDBNull(9) ? Convert.ToDecimal(reader.GetDouble(9)) : 150;
                    totalDollar = rate > 0 ? totalDinar / (rate * 10) : 0;
                }

                Invoices.Add(new InvoiceHeader
                {
                    Id = reader.GetInt32(0),
                    InvoiceNumber = reader.IsDBNull(1)
                        ? reader.GetInt32(0).ToString(CultureInfo.InvariantCulture)
                        : reader.GetString(1),
                    CustomerName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Phone = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Date = reader.GetDateTime(4),
                    TotalAmount = (double)totalDinar,
                    TotalAmountDollar = (double)totalDollar,
                    InvoiceType = reader.IsDBNull(6) ? "وصل محل" : reader.GetString(6),
                    Currency = reader.IsDBNull(7) ? "IQD" : reader.GetString(7)
                });
            }

            dgvInvoices.ItemsSource = null;
            dgvInvoices.ItemsSource = Invoices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل تحميل قائمة الفواتير.");
            _dialog.ShowError($"خطأ: {ex.Message}");
        }
    }

    private async void BtnDeleteInvoice_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not InvoiceHeader selected)
            return;

        if (!_dialog.Confirm($"حجي متأكد تريد تحذف الوصل رقم ({selected.Id})؟", "تأكيد الحذف"))
            return;

        try
        {
            await using var connection = await _factory.CreateOpenAsync().ConfigureAwait(true);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Invoices WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", selected.Id);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(true);

            Invoices.Remove(selected);
            ShowToast("تم حذف الفاتورة بنجاح", "#10B981");
            _logger.LogInformation("تم حذف الفاتورة Id={Id}", selected.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل حذف الفاتورة Id={Id}", selected.Id);
            _dialog.ShowError($"صار خطأ بالحذف: {ex.Message}");
        }
    }

    private void BtnViewDetails_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not InvoiceHeader selected)
            return;

        try
        {
            var editorPage = (InvoiceEditorView)_services.GetService(typeof(InvoiceEditorView))!;
            editorPage.LoadInvoiceForEdit(selected.Id);
            _navigation.NavigateTo(editorPage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل فتح الفاتورة Id={Id}", selected.Id);
            _dialog.ShowError($"خطأ في فتح الفاتورة: {ex.Message}");
        }
    }

    private async void txtSearchInvoice_TextChanged(object sender, TextChangedEventArgs e)
        => await LoadInvoicesAsync(txtSearchInvoice.Text).ConfigureAwait(true);

    private static void ShowToast(string message, string colorHex = "#EF4444")
    {
        Window toast = new()
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            SizeToContent = SizeToContent.WidthAndHeight,
            Topmost = true,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(25, 12, 25, 12),
                Child = new TextBlock
                {
                    Text = message, Foreground = Brushes.White,
                    FontSize = 16, FontWeight = FontWeights.Bold
                }
            }
        };
        toast.Show();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) => { toast.Close(); timer.Stop(); };
        timer.Start();
    }
}
