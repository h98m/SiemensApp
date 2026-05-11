using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using SiemensApp.ViewModels;

namespace SiemensApp;

/// <summary>
/// نافذة تسجيل الدخول. كل المنطق في <see cref="LoginViewModel"/>؛ الكود الخلفي محدود
/// بالجوانب التي لا يدعمها MVVM مباشرة (تحريك النافذة، PasswordBox آمن).
/// </summary>
public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;

        // ربط ViewModel
        DataContext = _viewModel;
        _viewModel.ShakeRequested = OnShakeRequested;
        _viewModel.CloseRequested = Close;

        Loaded += (_, _) => txtPassword.Focus();
    }

    /// <summary>تحريك النافذة بالماوس.</summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    /// <summary>
    /// PasswordBox لا يدعم الـ binding المباشر لخاصية Password لأسباب أمنية.
    /// نمرّر القيمة يدوياً إلى الـ ViewModel.
    /// </summary>
    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb)
            _viewModel.Password = pb.Password;
    }

    /// <summary>تحديث تأكيد كلمة المرور (وضع أول تشغيل).</summary>
    private void OnConfirmPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb)
            _viewModel.ConfirmPassword = pb.Password;
    }

    private void OnShakeRequested()
    {
        if (Resources["ShakeAnimation"] is Storyboard shake)
            shake.Begin();

        // مسح كلمة المرور بصرياً وإعادة التركيز
        txtPassword.Clear();
        txtConfirmPassword.Clear();
        txtPassword.Focus();
    }
}
