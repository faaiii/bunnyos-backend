using System;
using System.Threading;
using System.Threading.Tasks;
using BunnyOS.Application.Interfaces;
using BunnyOS.Domain.Common;
using BunnyOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BunnyOS.Infrastructure.Persistence;

public class BunnyDbContext : DbContext, IBunnyDbContext
{
    private readonly ITenantContext _tenantContext;

    public BunnyDbContext(DbContextOptions<BunnyDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<PlatformNotification> PlatformNotifications => Set<PlatformNotification>();
    public DbSet<AiUsageRecord> AiUsageRecords => Set<AiUsageRecord>();
    public DbSet<PlatformAuditLog> PlatformAuditLogs => Set<PlatformAuditLog>();
    public DbSet<OtpVerification> OtpVerifications => Set<OtpVerification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global Multi-Tenant Query Filters
        modelBuilder.Entity<User>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<Category>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<Product>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<Customer>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<Supplier>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<Sale>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<SaleItem>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<Expense>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<StockMovement>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<AuditLog>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);

        // Enum to String mapping for Supabase / PostgreSQL varchar columns
        modelBuilder.Entity<User>(b =>
        {
            b.Property(u => u.Role).HasConversion<string>();
        });

        // Precision configurations
        modelBuilder.Entity<Product>(b =>
        {
            b.Property(p => p.CostPrice).HasPrecision(18, 2);
            b.Property(p => p.SellingPrice).HasPrecision(18, 2);
            b.Property(p => p.StockQuantity).HasPrecision(18, 3);
            b.Property(p => p.LowStockThreshold).HasPrecision(18, 3);
            b.HasIndex(p => new { p.TenantId, p.Sku }).IsUnique();
        });

        modelBuilder.Entity<Sale>(b =>
        {
            b.Property(s => s.Status).HasConversion<string>();
            b.Property(s => s.Subtotal).HasPrecision(18, 2);
            b.Property(s => s.DiscountAmount).HasPrecision(18, 2);
            b.Property(s => s.TaxAmount).HasPrecision(18, 2);
            b.Property(s => s.TotalAmount).HasPrecision(18, 2);
            b.Property(s => s.PaidAmount).HasPrecision(18, 2);
            b.Property(s => s.DueAmount).HasPrecision(18, 2);
            b.HasIndex(s => new { s.TenantId, s.InvoiceNumber }).IsUnique();
            b.HasIndex(s => new { s.TenantId, s.IdempotencyKey }).IsUnique();
        });

        modelBuilder.Entity<SaleItem>(b =>
        {
            b.Property(si => si.UnitCostPrice).HasPrecision(18, 2);
            b.Property(si => si.UnitSellingPrice).HasPrecision(18, 2);
            b.Property(si => si.Quantity).HasPrecision(18, 3);
            b.Property(si => si.TotalPrice).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Customer>(b =>
        {
            b.Property(c => c.TotalPurchases).HasPrecision(18, 2);
            b.Property(c => c.TotalPaid).HasPrecision(18, 2);
            b.Property(c => c.CurrentDue).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Supplier>(b =>
        {
            b.Property(s => s.TotalBilled).HasPrecision(18, 2);
            b.Property(s => s.TotalPaid).HasPrecision(18, 2);
            b.Property(s => s.CurrentDue).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Expense>(b =>
        {
            b.Property(e => e.Amount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<StockMovement>(b =>
        {
            b.Property(sm => sm.QuantityChange).HasPrecision(18, 3);
            b.Property(sm => sm.PreviousQuantity).HasPrecision(18, 3);
            b.Property(sm => sm.NewQuantity).HasPrecision(18, 3);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<TenantEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.TenantId == Guid.Empty && _tenantContext.TenantId != Guid.Empty)
                {
                    entry.Entity.TenantId = _tenantContext.TenantId;
                }
                entry.Entity.CreatedAtUtc = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
