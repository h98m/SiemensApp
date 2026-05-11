using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiemensApp.Data;
using SiemensApp.Models;

namespace SiemensApp.Services.Repositories;

/// <summary>تنفيذ EF Core لـ <see cref="IProductRepository"/>.</summary>
public sealed class ProductRepository : IProductRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<ProductRepository> _logger;

    public ProductRepository(
        IDbContextFactory<AppDbContext> contextFactory,
        ILogger<ProductRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await ctx.Products.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(barcode))
            return null;

        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await ctx.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Barcode == barcode, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);

        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Product added: {Barcode}", product.Barcode);
    }

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);

        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        ctx.Products.Update(product);
        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Product updated: {Barcode}", product.Barcode);
    }

    public async Task DeleteAsync(string barcode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(barcode))
            return;

        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var existing = await ctx.Products
            .FirstOrDefaultAsync(p => p.Barcode == barcode, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
            return;

        ctx.Products.Remove(existing);
        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Product deleted: {Barcode}", barcode);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await ctx.Products.CountAsync(cancellationToken).ConfigureAwait(false);
    }
}
