using SiemensApp.Services;

namespace SiemensApp.Tests;

/// <summary>
/// اختبارات تحقق لخوارزمية تجزئة كلمات المرور (PBKDF2/HMACSHA256).
/// </summary>
public class PasswordHasherTests
{
    [Fact]
    public void GenerateSalt_ProducesNonEmptyAndUniqueValues()
    {
        string s1 = PasswordHasher.GenerateSalt();
        string s2 = PasswordHasher.GenerateSalt();

        Assert.False(string.IsNullOrWhiteSpace(s1));
        Assert.False(string.IsNullOrWhiteSpace(s2));
        Assert.NotEqual(s1, s2);
    }

    [Fact]
    public void Hash_ReturnsDeterministicValueForSameInput()
    {
        string salt = PasswordHasher.GenerateSalt();
        string hash1 = PasswordHasher.Hash("199426", salt);
        string hash2 = PasswordHasher.Hash("199426", salt);

        Assert.Equal(hash1, hash2);
        Assert.False(string.IsNullOrEmpty(hash1));
    }

    [Fact]
    public void Hash_ReturnsDifferentValueForDifferentSalt()
    {
        string s1 = PasswordHasher.GenerateSalt();
        string s2 = PasswordHasher.GenerateSalt();

        string h1 = PasswordHasher.Hash("199426", s1);
        string h2 = PasswordHasher.Hash("199426", s2);

        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void Verify_AcceptsCorrectPassword()
    {
        string salt = PasswordHasher.GenerateSalt();
        string hash = PasswordHasher.Hash("MySecret#2024", salt);

        Assert.True(PasswordHasher.Verify("MySecret#2024", salt, hash));
    }

    [Fact]
    public void Verify_RejectsWrongPassword()
    {
        string salt = PasswordHasher.GenerateSalt();
        string hash = PasswordHasher.Hash("MySecret#2024", salt);

        Assert.False(PasswordHasher.Verify("wrongpassword", salt, hash));
        Assert.False(PasswordHasher.Verify("MySecret#2025", salt, hash));
        Assert.False(PasswordHasher.Verify("", salt, hash));
    }

    [Fact]
    public void Verify_HandlesEmptyAndNullishInputs()
    {
        string salt = PasswordHasher.GenerateSalt();
        string hash = PasswordHasher.Hash("a", salt);

        Assert.False(PasswordHasher.Verify("", salt, hash));
    }

    [Fact]
    public void Verify_ReturnsFalseForCorruptedHash()
    {
        string salt = PasswordHasher.GenerateSalt();
        string hash = PasswordHasher.Hash("199426", salt);

        // قلب آخر حرف لإفساد التجزئة
        char last = hash[^1];
        char replacement = last == 'A' ? 'B' : 'A';
        string corrupted = string.Concat(hash.AsSpan(0, hash.Length - 1), replacement.ToString());

        Assert.False(PasswordHasher.Verify("199426", salt, corrupted));
    }
}
