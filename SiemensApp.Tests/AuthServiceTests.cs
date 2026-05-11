using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SiemensApp.Configuration;
using SiemensApp.Services;

namespace SiemensApp.Tests;

/// <summary>
/// اختبارات لمنطق <see cref="AuthService"/> — تتحقق من النجاح، رفض كلمة خاطئة،
/// وإغلاق الحساب بعد عدد من المحاولات الفاشلة.
/// </summary>
public class AuthServiceTests
{
    private static AuthService CreateService(string defaultPassword = "199426", int maxAttempts = 3, int lockoutMinutes = 1)
    {
        var options = Options.Create(new AuthOptions
        {
            DefaultPassword = defaultPassword,
            MaxFailedAttempts = maxAttempts,
            LockoutMinutes = lockoutMinutes
        });
        var settings = new InMemoryUserSettingsStore();
        return new AuthService(settings, options, NullLogger<AuthService>.Instance);
    }

    [Fact]
    public void Login_WithDefaultPassword_FirstRunSucceedsAndPersistsHash()
    {
        var service = CreateService();
        var result = service.Login("199426");
        Assert.Equal(LoginResult.Success, result);
    }

    [Fact]
    public void Login_WithWrongPassword_ReturnsInvalid()
    {
        var service = CreateService();
        var result = service.Login("badpw");
        Assert.Equal(LoginResult.InvalidPassword, result);
    }

    [Fact]
    public void Login_AfterMaxFailures_LocksOut()
    {
        var service = CreateService(maxAttempts: 3);

        // أول محاولتين فاشلتين → InvalidPassword
        Assert.Equal(LoginResult.InvalidPassword, service.Login("x"));
        Assert.Equal(LoginResult.InvalidPassword, service.Login("y"));

        // المحاولة الثالثة الفاشلة تتسبب بالحظر
        Assert.Equal(LoginResult.LockedOut, service.Login("z"));
        Assert.True(service.IsLockedOut);

        // أي محاولة جديدة بعد الحظر تعيد LockedOut حتى لو كانت كلمة المرور صحيحة
        Assert.Equal(LoginResult.LockedOut, service.Login("199426"));
    }

    [Fact]
    public void ChangePassword_UpdatesStoredHash()
    {
        var service = CreateService();
        service.Login("199426");

        service.ChangePassword("new-password-2024");

        // كلمة المرور القديمة لم تعد صالحة
        Assert.Equal(LoginResult.InvalidPassword, service.Login("199426"));
    }

    /// <summary>متجر إعدادات في الذاكرة لاستخدام الاختبارات (لا يلامس القرص).</summary>
    private sealed class InMemoryUserSettingsStore : IUserSettingsStore
    {
        private UserSettings _settings = new();

        public string FilePath => ":memory:";

        public UserSettings Load() => _settings;

        public void Save(UserSettings settings) => _settings = settings;
    }
}
