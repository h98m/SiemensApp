using SiemensApp.Models;

namespace SiemensApp.Services.Repositories;

/// <summary>
/// مستودع المنتجات (Products) — يستخدم EF Core عبر <see cref="Data.AppDbContext"/>.
/// المفتاح الأساسي: <see cref="Product.Barcode"/>.
/// </summary>
public interface IProductRepository
{
    /// <summary>الحصول على كل المنتجات.</summary>
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>الحصول على منتج بحسب الباركود (أو null إن لم يوجد).</summary>
    Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);

    /// <summary>إضافة منتج جديد. يرمي إذا كان الباركود موجوداً مسبقاً.</summary>
    Task AddAsync(Product product, CancellationToken cancellationToken = default);

    /// <summary>تحديث منتج موجود (بحسب الباركود). يرمي إذا لم يوجد.</summary>
    Task UpdateAsync(Product product, CancellationToken cancellationToken = default);

    /// <summary>حذف منتج بحسب الباركود. لا يرمي إذا لم يوجد.</summary>
    Task DeleteAsync(string barcode, CancellationToken cancellationToken = default);

    /// <summary>عدد المنتجات الكلي.</summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
