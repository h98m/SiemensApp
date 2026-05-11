namespace SiemensApp.Models;

/// <summary>عنصر في المخزن الداخلي (جدول InternalStock في SQLite).</summary>
public class InternalStockItem
{
    public string Barcode { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string Currency { get; set; } = "دينار عراقي";
}
