using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SiemensApp.Services;

/// <summary>
/// تنفيذ خدمة التنقّل بين الصفحات داخل النافذة الرئيسية.
/// تُحقَن في الـ ViewModels لتُتيح فتح صفحات جديدة دون اقتران مباشر بـ MainWindow.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<NavigationService> _logger;

    /// <summary>الحاوية (ContentControl/Frame) التي تستقبل المحتوى — تُسجَّل من MainWindow عند تحميلها.</summary>
    public ContentControl? Host { get; set; }

    public NavigationService(IServiceProvider services, ILogger<NavigationService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public void NavigateTo<TView>() where TView : UserControl
    {
        var view = _services.GetRequiredService<TView>();
        NavigateTo(view);
    }

    public void NavigateTo(UserControl view)
    {
        if (Host is null)
        {
            _logger.LogWarning("لا توجد Host مسجَّلة في NavigationService — تجاهل التنقّل إلى {View}",
                view.GetType().Name);
            return;
        }

        _logger.LogDebug("تنقّل إلى {View}", view.GetType().Name);
        Host.Content = view;
    }
}
