using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SiemensApp.Services;

/// <summary>
/// مخزن إعدادات المستخدم (يُحفظ في AppData/Roaming/SiemensApp/user-settings.json).
/// يُستخدم لحفظ الهاش الفعلي لكلمة المرور وأيّ إعدادات حساسة لا تُكتب في appsettings.json.
/// </summary>
public sealed class UserSettings
{
    [JsonPropertyName("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [JsonPropertyName("passwordSalt")]
    public string PasswordSalt { get; set; } = string.Empty;

    [JsonPropertyName("lastLoginUtc")]
    public DateTime? LastLoginUtc { get; set; }
}

public interface IUserSettingsStore
{
    UserSettings Load();
    void Save(UserSettings settings);
    string FilePath { get; }
}

public sealed class UserSettingsStore : IUserSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string FilePath { get; }

    public UserSettingsStore()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "SiemensApp");
        Directory.CreateDirectory(folder);
        FilePath = Path.Combine(folder, "user-settings.json");
    }

    public UserSettings Load()
    {
        if (!File.Exists(FilePath))
            return new UserSettings();

        try
        {
            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<UserSettings>(json, JsonOptions) ?? new UserSettings();
        }
        catch
        {
            // ملف مكسور — نُرجع إعدادات افتراضية لتجنّب إيقاف التطبيق
            return new UserSettings();
        }
    }

    public void Save(UserSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}
