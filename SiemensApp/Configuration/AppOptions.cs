namespace SiemensApp.Configuration;

/// <summary>إعدادات عامة للتطبيق (تُقرأ من قسم "App" في appsettings.json).</summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    public string Title { get; set; } = "SiemensApp";
    public string Version { get; set; } = "2.0.0";
    public decimal DefaultDollarRate { get; set; } = 150m;
}

/// <summary>إعدادات قاعدة البيانات.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string FileName { get; set; } = "SiemensData.db";
    public bool EnableSensitiveDataLogging { get; set; }

    /// <summary>المسار الكامل لملف قاعدة البيانات بجوار ملف التشغيل.</summary>
    public string GetFullPath() => System.IO.Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>سلسلة اتصال SQLite الكاملة.</summary>
    public string GetConnectionString() => $"Data Source={GetFullPath()}";
}

/// <summary>إعدادات المصادقة وحماية تسجيل الدخول.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>كلمة المرور الافتراضية المستخدمة عند أول تشغيل (يجب تغييرها).</summary>
    public string DefaultPassword { get; set; } = "199426";

    /// <summary>هاش كلمة المرور (Base64) — يُحفظ في user-config بعد أول تشغيل.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>الملح (Base64) المرافق للهاش.</summary>
    public string PasswordSalt { get; set; } = string.Empty;

    /// <summary>الحد الأقصى لمحاولات الدخول الفاشلة قبل الحظر المؤقت.</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>مدة الحظر المؤقت بالدقائق.</summary>
    public int LockoutMinutes { get; set; } = 5;
}
