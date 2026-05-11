using System.Windows.Controls;

namespace SiemensApp.Services;

/// <summary>
/// خدمة تنقّل بين الصفحات داخل النافذة الرئيسية.
/// تُحقن في الـ ViewModels لتُمكّنها من فتح صفحات أخرى دون اقتران مباشر بـ MainWindow.
/// </summary>
public interface INavigationService
{
    void NavigateTo<TView>() where TView : UserControl;
    void NavigateTo(UserControl view);
}
