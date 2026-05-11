using SiemensApp.Models;

namespace SiemensApp.Services.Repositories;

/// <summary>
/// مستودع الديون. المفتاح الأساسي: <see cref="Debt.Name"/>.
/// </summary>
public interface IDebtRepository
{
    /// <summary>كل الديون.</summary>
    Task<IReadOnlyList<Debt>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>دين بحسب الاسم (أو null).</summary>
    Task<Debt?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>إضافة دين جديد.</summary>
    Task AddAsync(Debt debt, CancellationToken cancellationToken = default);

    /// <summary>تحديث قيمة دين موجود.</summary>
    Task UpdateAsync(Debt debt, CancellationToken cancellationToken = default);

    /// <summary>حذف دين بحسب الاسم.</summary>
    Task DeleteAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>المجموع الكلي للديون بحسب العملة.</summary>
    Task<decimal> GetTotalByCurrencyAsync(string currency, CancellationToken cancellationToken = default);
}
