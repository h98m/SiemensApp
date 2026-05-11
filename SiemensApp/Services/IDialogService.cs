using System.Windows;

namespace SiemensApp.Services;

/// <summary>
/// خدمة عرض الحوارات (يمكن استبدالها بـ Mock في الاختبارات).
/// تحلّ محل MessageBox.Show المتناثرة في الكود القديم.
/// </summary>
public interface IDialogService
{
    void ShowInfo(string message, string title = "معلومات");
    void ShowWarning(string message, string title = "تنبيه");
    void ShowError(string message, string title = "خطأ");

    /// <summary>سؤال نعم/لا. يُرجع true إذا اختار المستخدم "نعم".</summary>
    bool Confirm(string message, string title = "تأكيد");
}

public sealed class DialogService : IDialogService
{
    public void ShowInfo(string message, string title = "معلومات") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowWarning(string message, string title = "تنبيه") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void ShowError(string message, string title = "خطأ") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool Confirm(string message, string title = "تأكيد") =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            == MessageBoxResult.Yes;
}
