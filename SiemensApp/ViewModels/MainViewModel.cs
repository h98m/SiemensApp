using System.Windows.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SiemensApp.Mvvm;
using SiemensApp.Services;
using SiemensApp.Views;

namespace SiemensApp.ViewModels;

/// <summary>
/// ViewModel للنافذة الرئيسية — مسؤول عن تنقّل بين الصفحات.
/// </summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly ILogger<MainViewModel> _logger;

    public MainViewModel(INavigationService navigation, ILogger<MainViewModel> logger)
    {
        _navigation = navigation;
        _logger = logger;
    }

    [RelayCommand]
    private void NavigateInvoice() => _navigation.NavigateTo<InvoiceView>();

    [RelayCommand]
    private void NavigateStorage() => _navigation.NavigateTo<StorageView>();

    [RelayCommand]
    private void NavigateInternalStorage() => _navigation.NavigateTo<InternalStorageView>();

    [RelayCommand]
    private void NavigateAddProduct() => _navigation.NavigateTo<AddProductView>();

    [RelayCommand]
    private void NavigateDebtsMe() => _navigation.NavigateTo<DebtsMeView>();

    [RelayCommand]
    private void NavigateDebtsThem() => _navigation.NavigateTo<DebtsThemView>();

    [RelayCommand]
    private void NavigateInvoices() => _navigation.NavigateTo<InvoicesListView>();

    /// <summary>تُستدعى من الـ View عند تحميل الـ ContentControl لربطه بـ NavigationService.</summary>
    public void RegisterHost(ContentControl host)
    {
        if (_navigation is NavigationService nav)
            nav.Host = host;
    }

    /// <summary>التنقّل الافتراضي عند فتح النافذة الرئيسية.</summary>
    public void NavigateInitial()
    {
        _logger.LogDebug("تحميل الصفحة الافتراضية (الفاتورة).");
        _navigation.NavigateTo<InvoiceView>();
    }
}
