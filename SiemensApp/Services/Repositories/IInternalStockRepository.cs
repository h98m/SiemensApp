using SiemensApp.Models;

namespace SiemensApp.Services.Repositories;

/// <summary>
/// مستودع المخزن الداخلي (جدول InternalStock في SQLite). المفتاح الأساسي:
/// <see cref="InternalStockItem.Barcode"/>.
/// </summary>
public interface IInternalStockRepository
{
    Task<IReadOnlyList<InternalStockItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<InternalStockItem?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);

    Task UpsertAsync(InternalStockItem item, CancellationToken cancellationToken = default);

    Task DeleteAsync(string barcode, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
