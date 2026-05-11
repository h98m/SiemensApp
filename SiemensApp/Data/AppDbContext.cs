using Microsoft.EntityFrameworkCore;
using SiemensApp.Models;

namespace SiemensApp.Data;

/// <summary>سياق قاعدة بيانات Entity Framework Core الرئيسي.</summary>
public class AppDbContext : DbContext
{
    /// <summary>منشئ يستخدمه DbContextFactory في DI الحديث.</summary>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>منشئ افتراضي للتوافق العكسي مع الكود القديم (يستخدم SiemensData.db بجوار التطبيق).</summary>
    public AppDbContext() { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<SaleRecord> Sales => Set<SaleRecord>();
    public DbSet<Debt> Debts => Set<Debt>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // إذا تم تكوينه عبر DI/DbContextOptions نتجنّب إعادة الضبط
        if (optionsBuilder.IsConfigured)
            return;

        string dbPath = System.IO.Path.Combine(AppContext.BaseDirectory, "SiemensData.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().HasKey(p => p.Barcode);
        modelBuilder.Entity<SaleRecord>().HasKey(s => s.Id);
        modelBuilder.Entity<Debt>().HasKey(d => d.Name);

        modelBuilder.Entity<SaleRecord>()
            .OwnsMany(s => s.Items, a =>
            {
                a.WithOwner().HasForeignKey("SaleRecordId");
                a.Property<int>("Id");
                a.HasKey("Id");
            });
    }
}
