using SiemensApp.Helpers;

namespace SiemensApp.Tests;

/// <summary>
/// اختبارات تحقق من سلوك أداة التفقيط (تحويل الأرقام إلى كلمات عربية).
/// </summary>
public class TafqeetToolTests
{
    [Fact]
    public void Convert_Zero_ReturnsZeroLabelWithCurrency()
    {
        string result = TafqeetTool.Convert(0m);
        Assert.Equal("صفر دينار عراقي", result);
    }

    [Fact]
    public void Convert_Negative_PrefixesWithMinusWord()
    {
        string result = TafqeetTool.Convert(-50m);
        Assert.StartsWith("سالب", result);
        Assert.Contains("خمسون", result);
        Assert.Contains("دينار عراقي", result);
    }

    [Theory]
    [InlineData(1, "واحد")]
    [InlineData(2, "اثنان")]
    [InlineData(10, "عشرة")]
    [InlineData(11, "أحد عشر")]
    [InlineData(20, "عشرون")]
    [InlineData(21, "واحد و عشرون")]
    [InlineData(100, "مائة")]
    [InlineData(200, "مائتان")]
    public void Convert_BasicNumbers_RendersExpectedWords(int amount, string expectedFragment)
    {
        string result = TafqeetTool.Convert(amount);
        Assert.Contains(expectedFragment, result);
    }

    [Fact]
    public void Convert_Thousand_UsesAlfPlural()
    {
        string result = TafqeetTool.Convert(1000m);
        Assert.Contains("ألف", result);
        Assert.Contains("دينار عراقي", result);
    }

    [Fact]
    public void Convert_Million_UsesMillionWord()
    {
        string result = TafqeetTool.Convert(1_000_000m);
        Assert.Contains("مليون", result);
    }

    [Fact]
    public void Convert_LargeRoundedNumber_RoundsUpFractionTo100()
    {
        // 1.999 يقرّب إلى 2 (الكسر يصبح 100 ثم يُرحّل)
        string result = TafqeetTool.Convert(1.999m);
        Assert.Contains("اثنان", result);
        Assert.DoesNotContain("فلس", result);
    }

    [Fact]
    public void Convert_WithFraction_AppendsSubCurrency()
    {
        string result = TafqeetTool.Convert(2.50m, currency: "دولار", subCurrency: "سنت");
        Assert.Contains("اثنان", result);
        Assert.Contains("سنت", result);
        Assert.Contains("دولار", result);
    }

    [Fact]
    public void Convert_DefaultsToIraqiDinar()
    {
        string result = TafqeetTool.Convert(5m);
        Assert.Contains("دينار عراقي", result);
    }

    [Fact]
    public void Convert_LargeNumber_ContainsBillionWord()
    {
        string result = TafqeetTool.Convert(1_500_000_000m);
        Assert.Contains("مليار", result);
    }

    [Fact]
    public void Convert_NegativeWithFraction_HandlesBothSignAndFraction()
    {
        string result = TafqeetTool.Convert(-12.34m);
        Assert.StartsWith("سالب", result);
        Assert.Contains("اثنا عشر", result);
    }
}
