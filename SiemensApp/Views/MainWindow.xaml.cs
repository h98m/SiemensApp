using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using SiemensApp.Services;
using SiemensApp.ViewModels;

namespace SiemensApp;

/// <summary>
/// النافذة الرئيسية للتطبيق. تستضيف القائمة الجانبية و<see cref="MainContentFrame"/> الذي
/// يعرض الصفحة الحالية. تستخدم <see cref="MainViewModel"/> لمنطق التنقّل عبر MVVM.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IDialogService _dialog;
    private readonly ILogger<MainWindow> _logger;

    public MainWindow(MainViewModel viewModel, IDialogService dialog, ILogger<MainWindow> logger)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _dialog = dialog;
        _logger = logger;
        DataContext = _viewModel;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // تسجيل الـ ContentControl مع NavigationService حتى تتمكن أوامر MVVM من التحكم فيه
        _viewModel.RegisterHost(MainContentFrame);
        _viewModel.NavigateInitial();
    }

    /// <summary>
    /// معالج التنقّل عبر RadioButtons في الـ XAML.
    /// يبقى موجوداً للحفاظ على XAML الموجودة ويُحوّل التنقّل إلى أوامر MVVM.
    /// </summary>
    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton btn)
            return;

        try
        {
            switch (btn.Name)
            {
                case "BtnInvoice":
                    _viewModel.NavigateInvoiceCommand.Execute(null);
                    break;
                case "BtnStorageEx":
                    _viewModel.NavigateStorageCommand.Execute(null);
                    break;
                case "BtnStorageIn":
                    _viewModel.NavigateInternalStorageCommand.Execute(null);
                    break;
                case "BtnAddProduct":
                    _viewModel.NavigateAddProductCommand.Execute(null);
                    break;
                case "BtnDebtsMe":
                    _viewModel.NavigateDebtsMeCommand.Execute(null);
                    break;
                case "BtnDebtsThem":
                    _viewModel.NavigateDebtsThemCommand.Execute(null);
                    break;
                case "BtnInvoices":
                    _viewModel.NavigateInvoicesCommand.Execute(null);
                    break;
                default:
                    _logger.LogWarning("زر تنقّل غير معروف: {Name}", btn.Name);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل التنقّل من الزر {Name}", btn.Name);
            _dialog.ShowError($"تعذر تحميل الصفحة: {ex.Message}");
        }
    }
}
