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
/// يدعم أيضاً وضع "أول تشغيل" حين يجب تعيين كلمة المرور قبل أوّل دخول.
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

        IsFirstRun = _authService.IsFirstRun;
        Title = IsFirstRun ? "تعيين كلمة المرور" : "تسجيل الدخول";
        SubmitButtonText = IsFirstRun ? "حـفـظ وفـتـح الـبـرنامج" : "فـتـح الـنـظـام";
        MinimumPasswordLength = _authService.MinimumPasswordLength;

        if (IsFirstRun)
        {
            _logger.LogInformation("أول تشغيل — سيُطلب من المستخدم تعيين كلمة مرور (الحد الأدنى: {Min}).",
                MinimumPasswordLength);
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private string _password = string.Empty;

    /// <summary>تأكيد كلمة المرور — يُستخدم فقط في وضع أول تشغيل.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isErrorVisible;

    /// <summary>هل التحقق جارٍ حالياً؟ (يُعطل الزر).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private bool _isBusy;

    /// <summary>هل المستخدم محظور حالياً؟</summary>
    [ObservableProperty]
    private bool _isLockedOut;

    /// <summary>هل نحن في وضع "أول تشغيل" (تعيين كلمة مرور)؟</summary>
    [ObservableProperty]
    private bool _isFirstRun;

    /// <summary>عنوان نافذة الدخول (يتغيّر حسب وضع أول تشغيل أو دخول عادي).</summary>
    [ObservableProperty]
    private string _title = "تسجيل الدخول";

    /// <summary>نص الزر الرئيسي.</summary>
    [ObservableProperty]
    private string _submitButtonText = "فـتـح الـنـظـام";

    /// <summary>الحد الأدنى لطول كلمة المرور (للعرض في رسالة المساعدة).</summary>
    public int MinimumPasswordLength { get; }

    /// <summary>إجراء تشغّله الـ View لطلب اهتزاز النافذة عند الفشل.</summary>
    public Action? ShakeRequested { get; set; }

    /// <summary>إجراء تشغّله الـ View لإغلاق النافذة بعد نجاح الدخول.</summary>
    public Action? CloseRequested { get; set; }

    private bool CanSubmit()
    {
        if (IsBusy || string.IsNullOrEmpty(Password))
            return false;

        if (IsFirstRun && string.IsNullOrEmpty(ConfirmPassword))
            return false;

        return true;
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private void Submit()
    {
        IsBusy = true;
        IsErrorVisible = false;
        ErrorMessage = string.Empty;

        try
        {
            if (IsFirstRun)
            {
                HandleInitialSetup();
            }
            else
            {
                HandleLogin();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ غير متوقع أثناء معالجة شاشة الدخول.");
            ErrorMessage = "حدث خطأ غير متوقع. راجع السجلات.";
            IsErrorVisible = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void HandleInitialSetup()
    {
        var result = _authService.SetInitialPassword(Password, ConfirmPassword);

        switch (result)
        {
            case SetupResult.Success:
                _logger.LogInformation("تمّ تعيين كلمة المرور لأول مرة. جارٍ الدخول للنظام.");

                // ندخل للنظام مباشرة بعد التعيين الناجح (UX أفضل من إعادة عرض شاشة الدخول).
                var loginAfterSetup = _authService.Login(Password);
                if (loginAfterSetup == LoginResult.Success)
                {
                    OpenMainWindow();
                    CloseRequested?.Invoke();
                }
                else
                {
                    // لا يُفترض أن يحصل، لكن نعالجها كاحتياط: نُحدث الـ UI لوضع الدخول العادي.
                    IsFirstRun = false;
                    Title = "تسجيل الدخول";
                    SubmitButtonText = "فـتـح الـنـظـام";
                    Password = string.Empty;
                    ConfirmPassword = string.Empty;
                    ErrorMessage = "تمّ الحفظ. يرجى إدخال كلمة المرور لمتابعة الدخول.";
                    IsErrorVisible = true;
                }
                break;

            case SetupResult.PasswordEmpty:
                ShowError("الرجاء إدخال كلمة المرور.");
                break;

            case SetupResult.PasswordTooShort:
                ShowError($"كلمة المرور قصيرة جداً. الحد الأدنى {MinimumPasswordLength} خانات.");
                break;

            case SetupResult.PasswordsDoNotMatch:
                ShowError("كلمتا المرور غير متطابقتين.");
                ConfirmPassword = string.Empty;
                break;

            case SetupResult.AlreadyConfigured:
                // حالة استثنائية: تمّ إعداد التطبيق من جلسة أخرى — نتحوّل لوضع الدخول العادي.
                IsFirstRun = false;
                Title = "تسجيل الدخول";
                SubmitButtonText = "فـتـح الـنـظـام";
                Password = string.Empty;
                ConfirmPassword = string.Empty;
                ShowError("تمّ إعداد التطبيق سابقاً. الرجاء تسجيل الدخول.");
                break;
        }
    }

    private void HandleLogin()
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
                ShowError($"كلمة المرور خاطئة (محاولات متبقية: {_authService.RemainingAttempts}).");
                Password = string.Empty;
                System.Media.SystemSounds.Asterisk.Play();
                break;

            case LoginResult.LockedOut:
                IsLockedOut = true;
                ShowError($"تم تعليق الحساب مؤقتاً. حاول بعد {_authService.LockoutSecondsRemaining} ثانية.");
                Password = string.Empty;
                break;

            case LoginResult.NotConfigured:
                // ربما حُذف ملف user-settings.json أثناء التشغيل — نُحوّل لوضع أول تشغيل.
                IsFirstRun = true;
                Title = "تعيين كلمة المرور";
                SubmitButtonText = "حـفـظ وفـتـح الـبـرنامج";
                Password = string.Empty;
                ConfirmPassword = string.Empty;
                ShowError("التطبيق يحتاج إلى تعيين كلمة مرور قبل الدخول.");
                break;
        }
    }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        IsErrorVisible = true;
        ShakeRequested?.Invoke();
    }

    [RelayCommand]
    private static void Exit() => Application.Current.Shutdown();

    private void OpenMainWindow()
    {
        var main = (MainWindow)_services.GetService(typeof(MainWindow))!;
        main.Show();
    }
}
