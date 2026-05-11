using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SiemensApp.Mvvm;
using SiemensApp.Services;
// MainWindow في الـ namespace SiemensApp مباشرة (وفقاً للـ XAML الأصلي).

namespace SiemensApp.ViewModels;

/// <summary>
/// ViewModel لشاشة تسجيل الدخول. يستخدم <see cref="IAuthService"/> للتحقق ويُتيح ربط
/// كلمة المرور وإظهار رسالة الخطأ عبر MVVM.
/// </summary>
public sealed partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly IServiceProvider _services;
    private readonly ILogger<LoginViewModel> _logger;

    public LoginViewModel(
        IAuthService authService,
        IServiceProvider services,
        ILogger<LoginViewModel> logger)
    {
        _authService = authService;
        _services = services;
        _logger = logger;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isErrorVisible;

    /// <summary>هل التحقق جارٍ حالياً؟ (يُعطل الزر).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private bool _isBusy;

    /// <summary>هل المستخدم محظور حالياً؟</summary>
    [ObservableProperty]
    private bool _isLockedOut;

    /// <summary>إجراء تشغّله الـ View لطلب اهتزاز النافذة عند الفشل.</summary>
    public Action? ShakeRequested { get; set; }

    /// <summary>إجراء تشغّله الـ View لإغلاق النافذة بعد نجاح الدخول.</summary>
    public Action? CloseRequested { get; set; }

    private bool CanLogin() => !IsBusy && !string.IsNullOrEmpty(Password);

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private void Login()
    {
        IsBusy = true;
        IsErrorVisible = false;
        ErrorMessage = string.Empty;

        try
        {
            var result = _authService.Login(Password);

            switch (result)
            {
                case LoginResult.Success:
                    _logger.LogInformation("تم تسجيل الدخول بنجاح.");
                    OpenMainWindow();
                    CloseRequested?.Invoke();
                    break;

                case LoginResult.InvalidPassword:
                    ErrorMessage = $"كلمة المرور خاطئة (محاولات متبقية: {_authService.RemainingAttempts}).";
                    IsErrorVisible = true;
                    Password = string.Empty;
                    System.Media.SystemSounds.Asterisk.Play();
                    ShakeRequested?.Invoke();
                    break;

                case LoginResult.LockedOut:
                    IsLockedOut = true;
                    ErrorMessage = $"تم تعليق الحساب مؤقتاً. حاول بعد {_authService.LockoutSecondsRemaining} ثانية.";
                    IsErrorVisible = true;
                    Password = string.Empty;
                    ShakeRequested?.Invoke();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ غير متوقع أثناء تسجيل الدخول.");
            ErrorMessage = "حدث خطأ غير متوقع. راجع السجلات.";
            IsErrorVisible = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static void Exit() => Application.Current.Shutdown();

    private void OpenMainWindow()
    {
        var main = (MainWindow)_services.GetService(typeof(MainWindow))!;
        main.Show();
    }
}
