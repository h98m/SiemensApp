namespace SiemensApp.Models;

/// <summary>عنصر في المخزن العام (جدول GlobalStock في SQLite).</summary>
public class GlobalStockItem
{
    public long Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal DefaultPrice { get; set; }
    public string Currency { get; set; } = "دينار عراقي";
    public string Category { get; set; } = string.Empty;
}
