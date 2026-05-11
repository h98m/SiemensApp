namespace SiemensApp.Models.Ui;

/// <summary>
/// مادة من المخزن الداخلي مع خصائص العرض (DisplayPrice مفلتر بالعملة).
/// نُقلت من Views/AddProductView.xaml.cs لتُشارك بين عدة شاشات.
/// </summary>
public sealed class InternalItem
{
    public string Barcode { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public double Price { get; set; }
    public int Quantity { get; set; }
    public string Currency { get; set; } = "دينار عراقي";
    public string DisplayPrice { get; set; } = string.Empty;
}
