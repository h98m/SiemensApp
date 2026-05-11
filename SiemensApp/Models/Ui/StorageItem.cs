namespace SiemensApp.Models.Ui;

/// <summary>عنصر في جدول المخزن الخارجي (GlobalStock) للعرض في الواجهة.</summary>
public sealed class StorageItem
{
    public string ProductName { get; set; } = string.Empty;
    public double DefaultPrice { get; set; }
    public string Currency { get; set; } = "دينار عراقي";
    public string DisplayPrice { get; set; } = string.Empty;
}
