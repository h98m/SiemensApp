using System.Security.Cryptography;
using System.Text;

namespace SiemensApp.Services;

/// <summary>
/// أداة تشفير كلمات المرور باستخدام PBKDF2 (HMACSHA256).
/// آمن، بطيء بما يكفي لمقاومة هجمات Brute force، ومدعوم رسمياً في .NET.
/// </summary>
public static class PasswordHasher
{
    private const int SaltBytes = 16;        // 128-bit
    private const int HashBytes = 32;        // 256-bit
    private const int Iterations = 100_000;  // عدد جولات PBKDF2

    /// <summary>إنشاء ملح عشوائي (Base64).</summary>
    public static string GenerateSalt()
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltBytes);
        return Convert.ToBase64String(saltBytes);
    }

    /// <summary>توليد هاش (Base64) لكلمة المرور باستخدام ملح معطى.</summary>
    public static string Hash(string password, string saltBase64)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentException.ThrowIfNullOrEmpty(saltBase64);

        byte[] salt = Convert.FromBase64String(saltBase64);
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

        using var pbkdf2 = new Rfc2898DeriveBytes(passwordBytes, salt, Iterations, HashAlgorithmName.SHA256);
        return Convert.ToBase64String(pbkdf2.GetBytes(HashBytes));
    }

    /// <summary>التحقق من تطابق كلمة المرور مع هاش وملح معطيين.</summary>
    public static bool Verify(string password, string saltBase64, string hashBase64)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(saltBase64) || string.IsNullOrEmpty(hashBase64))
            return false;

        try
        {
            string computed = Hash(password, saltBase64);
            // مقارنة آمنة ضد هجمات التوقيت
            byte[] a = Convert.FromBase64String(computed);
            byte[] b = Convert.FromBase64String(hashBase64);
            return CryptographicOperations.FixedTimeEquals(a, b);
        }
        catch
        {
            return false;
        }
    }
}
