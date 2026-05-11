namespace SiemensApp.Models;

/// <summary>كيان قاعدة البيانات للمنتج.</summary>
public class Product
{
    public string Barcode { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}
