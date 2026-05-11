namespace SiemensApp.Data;

/// <summary>
/// نموذج قراءة المنتج من ملف JSON الخارجي. أسماء الخصائص يجب أن تطابق الـ JSON.
/// </summary>
public class JsonProductModel
{
    public string Barcode { get; set; } = string.Empty;

    /// <summary>الوصف العربي (مثل: ميني كونتكتر).</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>كود القطعة (مثل: 3RH6122).</summary>
    public string Name { get; set; } = string.Empty;

    public double Price { get; set; }

    public int Quantity { get; set; }
}
