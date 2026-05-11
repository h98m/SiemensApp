namespace SiemensApp.Models.Ui;

/// <summary>
/// رأس الفاتورة المعروض في صفحة سجل الفواتير.
/// </summary>
public sealed class InvoiceHeader
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public double TotalAmount { get; set; }
    public double TotalAmountDollar { get; set; }
    public string InvoiceType { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;

    public string TotalInDinar => TotalAmount.ToString("N0") + " د.ع";
    public string TotalInDollar => TotalAmountDollar.ToString("N2") + " $";
    public string FullSummary => $"{TotalInDinar} | {TotalInDollar}";
}
