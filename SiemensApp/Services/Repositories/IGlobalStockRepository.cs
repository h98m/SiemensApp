using SiemensApp.Models;

namespace SiemensApp.Services.Repositories;

/// <summary>
/// مستودع المخزن العام (جدول GlobalStock في SQLite).
/// لا يستخدم EF لأن هذا الجدول خارج <see cref="Data.AppDbContext"/>؛ يستعمل ADO.NET مباشرة عبر
/// <see cref="ISqliteConnectionFactory"/>.
/// </summary>
public interface IGlobalStockRepository
{
    Task<IReadOnlyList<GlobalStockItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<GlobalStockItem?> GetByNameAsync(string productName, CancellationToken cancellationToken = default);

    Task<long> AddAsync(GlobalStockItem item, CancellationToken cancellationToken = default);

    Task UpdateAsync(GlobalStockItem item, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
