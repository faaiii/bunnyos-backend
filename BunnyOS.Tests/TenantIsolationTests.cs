using System;
using System.Linq;
using System.Threading.Tasks;
using BunnyOS.Domain.Entities;
using BunnyOS.Infrastructure.Persistence;
using BunnyOS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BunnyOS.Tests;

public class TenantIsolationTests
{
    [Fact]
    public async Task QueryFilter_PreventsCrossTenantDataLeakage()
    {
        var tenantContext = new TenantContext();
        var options = new DbContextOptionsBuilder<BunnyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // 1. Seed data for Tenant A and Tenant B
        using (var db = new BunnyDbContext(options, tenantContext))
        {
            tenantContext.SetTenant(tenantA);
            db.Products.Add(new Product { TenantId = tenantA, Name = "Tenant A Product", Sku = "SKU-A" });
            db.Expenses.Add(new Expense { TenantId = tenantA, Category = "Tea", Amount = 50m });

            tenantContext.SetTenant(tenantB);
            db.Products.Add(new Product { TenantId = tenantB, Name = "Tenant B Secret Product", Sku = "SKU-B" });
            db.Expenses.Add(new Expense { TenantId = tenantB, Category = "Rent", Amount = 15000m });

            await db.SaveChangesAsync();
        }

        // 2. Query as Tenant A
        using (var db = new BunnyDbContext(options, tenantContext))
        {
            tenantContext.SetTenant(tenantA);

            var products = await db.Products.ToListAsync();
            var expenses = await db.Expenses.ToListAsync();

            // Should ONLY see Tenant A's records
            Assert.Single(products);
            Assert.Equal("Tenant A Product", products[0].Name);

            Assert.Single(expenses);
            Assert.Equal(50m, expenses[0].Amount);

            // Tenant B's data MUST NOT exist in query result
            Assert.DoesNotContain(products, p => p.Name.Contains("Tenant B"));
            Assert.DoesNotContain(expenses, e => e.Amount == 15000m);
        }

        // 3. Query as Tenant B
        using (var db = new BunnyDbContext(options, tenantContext))
        {
            tenantContext.SetTenant(tenantB);

            var products = await db.Products.ToListAsync();
            var expenses = await db.Expenses.ToListAsync();

            // Should ONLY see Tenant B's records
            Assert.Single(products);
            Assert.Equal("Tenant B Secret Product", products[0].Name);

            Assert.Single(expenses);
            Assert.Equal(15000m, expenses[0].Amount);
        }
    }
}
