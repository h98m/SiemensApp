using SiemensApp.Models;

namespace SiemensApp.Services.Repositories;

/// <summary>مستودع سجلات المبيعات (Sales).</summary>
public interface ISaleRecordRepository
{
    /// <summary>كل سجلات المبيعات (مع البنود).</summary>
    Task<IReadOnlyList<SaleRecord>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>سجل مبيعات بحسب الـ Id.</summary>
    Task<SaleRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>صفحة من سجلات المبيعات مرتّبة من الأحدث للأقدم.</summary>
    /// <param name="pageIndex">رقم الصفحة (0-based).</param>
    /// <param name="pageSize">حجم الصفحة (عدد العناصر).</param>
    Task<IReadOnlyList<SaleRecord>> GetPageAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>إضافة فاتورة جديدة.</summary>
    Task AddAsync(SaleRecord sale, CancellationToken cancellationToken = default);

    /// <summary>حذف فاتورة بحسب Id.</summary>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>عدد الفواتير الكلي.</summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
