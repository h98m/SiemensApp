using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SiemensApp.Configuration;

namespace SiemensApp.Services;

public interface IAuthService
{
    /// <summary>محاولة تسجيل دخول. تُرجع نتيجة العملية.</summary>
    LoginResult Login(string password);

    /// <summary>تغيير كلمة المرور (يُحفظ الهاش في user-settings).</summary>
    void ChangePassword(string newPassword);

    /// <summary>عدد المحاولات الفاشلة المتبقية قبل الحظر.</summary>
    int RemainingAttempts { get; }

    /// <summary>هل الحساب محظور حالياً؟</summary>
    bool IsLockedOut { get; }

    /// <summary>الوقت المتبقي للحظر بالثواني (إذا كان IsLockedOut صحيحاً).</summary>
    int LockoutSecondsRemaining { get; }
}

public enum LoginResult
{
    /// <summary>تسجيل الدخول ناجح.</summary>
    Success,
    /// <summary>كلمة المرور خاطئة.</summary>
    InvalidPassword,
    /// <summary>الحساب محظور بسبب كثرة المحاولات الفاشلة.</summary>
    LockedOut
}

public sealed class AuthService : IAuthService
{
    private readonly IUserSettingsStore _store;
    private readonly ILogger<AuthService> _logger;
    private readonly AuthOptions _options;

    private int _failedAttempts;
    private DateTime? _lockoutUntilUtc;

    public AuthService(
        IUserSettingsStore store,
        IOptions<AuthOptions> options,
        ILogger<AuthService> logger)
    {
        _store = store;
        _logger = logger;
        _options = options.Value;
    }

    public int RemainingAttempts => Math.Max(0, _options.MaxFailedAttempts - _failedAttempts);

    public bool IsLockedOut =>
        _lockoutUntilUtc.HasValue && DateTime.UtcNow < _lockoutUntilUtc.Value;

    public int LockoutSecondsRemaining =>
        IsLockedOut ? Math.Max(0, (int)Math.Ceiling((_lockoutUntilUtc!.Value - DateTime.UtcNow).TotalSeconds)) : 0;

    public LoginResult Login(string password)
    {
        if (IsLockedOut)
        {
            _logger.LogWarning("محاولة دخول أثناء فترة الحظر. ثوانٍ متبقية: {Remaining}", LockoutSecondsRemaining);
            return LoginResult.LockedOut;
        }

        var settings = _store.Load();

        bool firstRun = string.IsNullOrEmpty(settings.PasswordHash) || string.IsNullOrEmpty(settings.PasswordSalt);

        bool isValid = firstRun
            ? string.Equals(password, _options.DefaultPassword, StringComparison.Ordinal)
            : PasswordHasher.Verify(password, settings.PasswordSalt, settings.PasswordHash);

        if (!isValid)
        {
            _failedAttempts++;
            _logger.LogWarning("فشل تسجيل الدخول. عدد المحاولات الفاشلة: {Failed}/{Max}",
                _failedAttempts, _options.MaxFailedAttempts);

            if (_failedAttempts >= _options.MaxFailedAttempts)
            {
                _lockoutUntilUtc = DateTime.UtcNow.AddMinutes(_options.LockoutMinutes);
                _failedAttempts = 0;
                _logger.LogWarning("تم تفعيل الحظر المؤقت لمدة {Minutes} دقيقة.", _options.LockoutMinutes);
                return LoginResult.LockedOut;
            }

            return LoginResult.InvalidPassword;
        }

        // عند أول تشغيل ناجح بكلمة المرور الافتراضية، نرحّلها إلى هاش مُلَمَّح فوراً
        if (firstRun)
        {
            _logger.LogInformation("أول تسجيل دخول ناجح — يتم ترحيل كلمة المرور إلى هاش PBKDF2.");
            ChangePassword(password);
        }

        settings.LastLoginUtc = DateTime.UtcNow;
        _store.Save(settings);

        _failedAttempts = 0;
        _lockoutUntilUtc = null;
        _logger.LogInformation("تسجيل دخول ناجح في {TimeUtc}", DateTime.UtcNow);
        return LoginResult.Success;
    }

    public void ChangePassword(string newPassword)
    {
        ArgumentException.ThrowIfNullOrEmpty(newPassword);

        var settings = _store.Load();
        settings.PasswordSalt = PasswordHasher.GenerateSalt();
        settings.PasswordHash = PasswordHasher.Hash(newPassword, settings.PasswordSalt);
        _store.Save(settings);

        _logger.LogInformation("تم تحديث كلمة المرور بنجاح. ملف الإعدادات: {Path}", _store.FilePath);
    }
}
