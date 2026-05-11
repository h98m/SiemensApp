using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SiemensApp.Configuration;
using SiemensApp.Services;

namespace SiemensApp.Tests;

/// <summary>
/// اختبارات لمنطق <see cref="AuthService"/> — تتحقق من النجاح، رفض كلمة خاطئة،
/// الحظر بعد محاولات فاشلة، تعيين كلمة المرور لأول مرة، وحالات الرفض المختلفة.
/// </summary>
public class AuthServiceTests
{
    private const int MinPasswordLength = 6;

    private static AuthService CreateService(
        int maxAttempts = 3,
        int lockoutMinutes = 1,
        int minPasswordLength = MinPasswordLength)
    {
        var options = Options.Create(new AuthOptions
        {
            MaxFailedAttempts = maxAttempts,
            LockoutMinutes = lockoutMinutes,
            MinimumPasswordLength = minPasswordLength
        });
        var settings = new InMemoryUserSettingsStore();
        return new AuthService(settings, options, NullLogger<AuthService>.Instance);
    }

    private static AuthService CreateConfiguredService(string initialPassword = "secret-pass")
    {
        var service = CreateService();
        var setup = service.SetInitialPassword(initialPassword, initialPassword);
        Assert.Equal(SetupResult.Success, setup);
        return service;
    }

    // ---------- وضع أول تشغيل ----------

    [Fact]
    public void IsFirstRun_NewService_ReturnsTrue()
    {
        var service = CreateService();
        Assert.True(service.IsFirstRun);
    }

    [Fact]
    public void Login_BeforeSetup_ReturnsNotConfigured()
    {
        var service = CreateService();
        var result = service.Login("anything");
        Assert.Equal(LoginResult.NotConfigured, result);
    }

    [Fact]
    public void SetInitialPassword_WithMatchingValidPassword_Succeeds()
    {
        var service = CreateService();
        var result = service.SetInitialPassword("my-secret-pw", "my-secret-pw");

        Assert.Equal(SetupResult.Success, result);
        Assert.False(service.IsFirstRun);
    }

    [Fact]
    public void SetInitialPassword_WithEmptyPassword_ReturnsPasswordEmpty()
    {
        var service = CreateService();
        var result = service.SetInitialPassword(string.Empty, string.Empty);
        Assert.Equal(SetupResult.PasswordEmpty, result);
        Assert.True(service.IsFirstRun);
    }

    [Fact]
    public void SetInitialPassword_BelowMinimumLength_ReturnsPasswordTooShort()
    {
        var service = CreateService(minPasswordLength: 6);
        var result = service.SetInitialPassword("abc", "abc");
        Assert.Equal(SetupResult.PasswordTooShort, result);
        Assert.True(service.IsFirstRun);
    }

    [Fact]
    public void SetInitialPassword_WithMismatch_ReturnsPasswordsDoNotMatch()
    {
        var service = CreateService();
        var result = service.SetInitialPassword("my-secret-pw", "other-pw-123");
        Assert.Equal(SetupResult.PasswordsDoNotMatch, result);
        Assert.True(service.IsFirstRun);
    }

    [Fact]
    public void SetInitialPassword_AfterAlreadyConfigured_ReturnsAlreadyConfigured()
    {
        var service = CreateConfiguredService();
        var result = service.SetInitialPassword("another-pw-456", "another-pw-456");
        Assert.Equal(SetupResult.AlreadyConfigured, result);
    }

    // ---------- وضع الدخول العادي ----------

    [Fact]
    public void Login_WithCorrectPassword_AfterSetup_ReturnsSuccess()
    {
        var service = CreateConfiguredService("my-secret-pw");
        var result = service.Login("my-secret-pw");
        Assert.Equal(LoginResult.Success, result);
    }

    [Fact]
    public void Login_WithWrongPassword_ReturnsInvalid()
    {
        var service = CreateConfiguredService("my-secret-pw");
        var result = service.Login("wrong-pw");
        Assert.Equal(LoginResult.InvalidPassword, result);
    }

    [Fact]
    public void Login_AfterMaxFailures_LocksOut()
    {
        var service = CreateConfiguredService("my-secret-pw");

        // محاولتان فاشلتان → InvalidPassword
        Assert.Equal(LoginResult.InvalidPassword, service.Login("x"));
        Assert.Equal(LoginResult.InvalidPassword, service.Login("y"));

        // الثالثة الفاشلة تتسبب بالحظر
        Assert.Equal(LoginResult.LockedOut, service.Login("z"));
        Assert.True(service.IsLockedOut);

        // أي محاولة جديدة بعد الحظر تعيد LockedOut حتى لو كانت صحيحة
        Assert.Equal(LoginResult.LockedOut, service.Login("my-secret-pw"));
    }

    [Fact]
    public void ChangePassword_UpdatesStoredHash()
    {
        var service = CreateConfiguredService("my-secret-pw");
        service.ChangePassword("new-password-2024");

        // كلمة المرور القديمة لم تعد صالحة
        Assert.Equal(LoginResult.InvalidPassword, service.Login("my-secret-pw"));

        // الجديدة تنجح
        Assert.Equal(LoginResult.Success, service.Login("new-password-2024"));
    }

    [Fact]
    public void MinimumPasswordLength_DefaultsToAtLeastOne()
    {
        var service = CreateService(minPasswordLength: 0);
        Assert.True(service.MinimumPasswordLength >= 1);
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
