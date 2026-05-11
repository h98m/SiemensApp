using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiemensApp.Data;
using SiemensApp.Models;

namespace SiemensApp.Services.Repositories;

/// <summary>تنفيذ EF Core لـ <see cref="IDebtRepository"/>.</summary>
public sealed class DebtRepository : IDebtRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<DebtRepository> _logger;

    public DebtRepository(
        IDbContextFactory<AppDbContext> contextFactory,
        ILogger<DebtRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Debt>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await ctx.Debts.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Debt?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await ctx.Debts
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Name == name, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(Debt debt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(debt);

        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        ctx.Debts.Add(debt);
        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Debt added: {Name}", debt.Name);
    }

    public async Task UpdateAsync(Debt debt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(debt);

        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        ctx.Debts.Update(debt);
        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Debt updated: {Name}", debt.Name);
    }

    public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(name))
            return;

        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var existing = await ctx.Debts
            .FirstOrDefaultAsync(d => d.Name == name, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
            return;

        ctx.Debts.Remove(existing);
        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Debt deleted: {Name}", name);
    }

    public async Task<decimal> GetTotalByCurrencyAsync(string currency, CancellationToken cancellationToken = default)
    {
        // SQLite لا يدعم Sum للـ decimal على مستوى الـ provider. نجلب الأرقام ونجمعها في الـ memory.
        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var amounts = await ctx.Debts
            .AsNoTracking()
            .Where(d => d.Currency == currency)
            .Select(d => d.Amount)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return amounts.Sum();
    }
}
