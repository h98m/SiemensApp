using System.Collections.Generic;

namespace SiemensApp.Models;

/// <summary>كيان قاعدة البيانات لسجل المبيعات.</summary>
public class SaleRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Date { get; set; } = DateTime.Now;
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public List<SaleItem> Items { get; set; } = [];
}

/// <summary>عنصر فاتورة مبيعات.</summary>
public class SaleItem
{
    public string Name { get; set; } = string.Empty;
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
}
