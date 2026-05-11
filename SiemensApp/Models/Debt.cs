namespace SiemensApp.Models;

/// <summary>كيان قاعدة البيانات للديون.</summary>
public class Debt
{
    public string Name { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "IQD";
    public string Notes { get; set; } = string.Empty;
}
