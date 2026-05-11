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

    /// <summary>
    /// تعيين كلمة المرور لأول مرة (عند أول تشغيل، حين لا توجد كلمة مرور محفوظة).
    /// تُرجع نتيجة تصف نجاح/فشل العملية.
    /// </summary>
    SetupResult SetInitialPassword(string newPassword, string confirmPassword);

    /// <summary>
    /// هل التطبيق يحتاج إلى إعداد أوّلي؟ (لا توجد كلمة مرور محفوظة بعد).
    /// </summary>
    bool IsFirstRun { get; }

    /// <summary>عدد المحاولات الفاشلة المتبقية قبل الحظر.</summary>
    int RemainingAttempts { get; }

    /// <summary>هل الحساب محظور حالياً؟</summary>
    bool IsLockedOut { get; }

    /// <summary>الوقت المتبقي للحظر بالثواني (إذا كان IsLockedOut صحيحاً).</summary>
    int LockoutSecondsRemaining { get; }

    /// <summary>الحد الأدنى المطلوب لطول كلمة المرور (للاستخدام في الـ UI).</summary>
    int MinimumPasswordLength { get; }
}

public enum LoginResult
{
    /// <summary>تسجيل الدخول ناجح.</summary>
    Success,
    /// <summary>كلمة المرور خاطئة.</summary>
    InvalidPassword,
    /// <summary>الحساب محظور بسبب كثرة المحاولات الفاشلة.</summary>
    LockedOut,
    /// <summary>التطبيق يحتاج إلى تعيين كلمة مرور أوّلية قبل الدخول.</summary>
    NotConfigured
}

/// <summary>نتيجة محاولة تعيين كلمة المرور الأولى.</summary>
public enum SetupResult
{
    /// <summary>تمّ تعيين كلمة المرور بنجاح.</summary>
    Success,
    /// <summary>كلمة المرور فارغة.</summary>
    PasswordEmpty,
    /// <summary>كلمة المرور أقصر من الحد الأدنى المطلوب.</summary>
    PasswordTooShort,
    /// <summary>كلمتا المرور غير متطابقتين.</summary>
    PasswordsDoNotMatch,
    /// <summary>المحاولة مرفوضة لأن التطبيق سبق وأن تمّ إعداده.</summary>
    AlreadyConfigured
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

    public int MinimumPasswordLength => Math.Max(1, _options.MinimumPasswordLength);

    public bool IsFirstRun
    {
        get
        {
            var settings = _store.Load();
            return string.IsNullOrEmpty(settings.PasswordHash) || string.IsNullOrEmpty(settings.PasswordSalt);
        }
    }

    public LoginResult Login(string password)
    {
        if (IsLockedOut)
        {
            _logger.LogWarning("محاولة دخول أثناء فترة الحظر. ثوانٍ متبقية: {Remaining}", LockoutSecondsRemaining);
            return LoginResult.LockedOut;
        }

        var settings = _store.Load();

        if (string.IsNullOrEmpty(settings.PasswordHash) || string.IsNullOrEmpty(settings.PasswordSalt))
        {
            // التطبيق لم يُعَدّ بعد — يجب على المستخدم تعيين كلمة المرور الأولى.
            _logger.LogInformation("محاولة دخول قبل تعيين كلمة المرور الأولى. التطبيق يحتاج إعداداً أوّلياً.");
            return LoginResult.NotConfigured;
        }

        bool isValid = PasswordHasher.Verify(password, settings.PasswordSalt, settings.PasswordHash);

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

        settings.LastLoginUtc = DateTime.UtcNow;
        _store.Save(settings);

        _failedAttempts = 0;
        _lockoutUntilUtc = null;
        _logger.LogInformation("تسجيل دخول ناجح في {TimeUtc}", DateTime.UtcNow);
        return LoginResult.Success;
    }

    public SetupResult SetInitialPassword(string newPassword, string confirmPassword)
    {
        if (!IsFirstRun)
        {
            _logger.LogWarning("محاولة تعيين كلمة مرور أولى بعد إعداد التطبيق سابقاً.");
            return SetupResult.AlreadyConfigured;
        }

        if (string.IsNullOrEmpty(newPassword))
            return SetupResult.PasswordEmpty;

        if (newPassword.Length < MinimumPasswordLength)
            return SetupResult.PasswordTooShort;

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            return SetupResult.PasswordsDoNotMatch;

        ChangePassword(newPassword);
        _logger.LogInformation("تمّ تعيين كلمة المرور الأولى بنجاح.");
        return SetupResult.Success;
    }

    public void ChangePassword(string newPassword)
    {
        ArgumentException.ThrowIfNullOrEmpty(newPassword);

        if (newPassword.Length < MinimumPasswordLength)
            throw new ArgumentException(
                $"كلمة المرور قصيرة جداً. الحد الأدنى {MinimumPasswordLength} حروف.",
                nameof(newPassword));

        var settings = _store.Load();
        settings.PasswordSalt = PasswordHasher.GenerateSalt();
        settings.PasswordHash = PasswordHasher.Hash(newPassword, settings.PasswordSalt);
        _store.Save(settings);

        _logger.LogInformation("تم تحديث كلمة المرور بنجاح. ملف الإعدادات: {Path}", _store.FilePath);
    }
}
