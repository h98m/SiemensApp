using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiemensApp.Data;
using SiemensApp.Models;

namespace SiemensApp.Services.Repositories;

/// <summary>تنفيذ EF Core لـ <see cref="ISaleRecordRepository"/>.</summary>
public sealed class SaleRecordRepository : ISaleRecordRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<SaleRecordRepository> _logger;

    public SaleRecordRepository(
        IDbContextFactory<AppDbContext> contextFactory,
        ILogger<SaleRecordRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SaleRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await ctx.Sales
            .AsNoTracking()
            .OrderByDescending(s => s.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<SaleRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await ctx.Sales
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SaleRecord>> GetPageAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        if (pageIndex < 0) pageIndex = 0;
        if (pageSize <= 0) pageSize = 50;

        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await ctx.Sales
            .AsNoTracking()
            .OrderByDescending(s => s.Date)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(SaleRecord sale, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sale);

        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        ctx.Sales.Add(sale);
        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Sale added: {Id} (items={Count})", sale.Id, sale.Items.Count);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
            return;

        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var existing = await ctx.Sales
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
            return;

        ctx.Sales.Remove(existing);
        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Sale deleted: {Id}", id);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await ctx.Sales.CountAsync(cancellationToken).ConfigureAwait(false);
    }
}
