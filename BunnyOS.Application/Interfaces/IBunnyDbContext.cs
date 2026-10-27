using System;
using System.Threading;
using System.Threading.Tasks;
using BunnyOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BunnyOS.Application.Interfaces;

public interface ITenantContext
{
    Guid TenantId { get; }
    Guid? UserId { get; }
    string? UserRole { get; }
    void SetTenant(Guid tenantId, Guid? userId = null, string? role = null);
}

public interface IJwtTokenGenerator
{
    string GenerateToken(User user, Tenant tenant);
}

public interface IBunnyDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<User> Users { get; }
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<Sale> Sales { get; }
    DbSet<SaleItem> SaleItems { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
