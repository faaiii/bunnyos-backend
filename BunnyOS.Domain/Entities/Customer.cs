using System;
using System.Collections.Generic;
using BunnyOS.Domain.Common;

namespace BunnyOS.Domain.Entities;

public class Customer : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public decimal TotalPurchases { get; set; } = 0m;
    public decimal TotalPaid { get; set; } = 0m;
    public decimal CurrentDue { get; set; } = 0m;

    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
}

public class Supplier : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string? Address { get; set; }
    public decimal TotalBilled { get; set; } = 0m;
    public decimal TotalPaid { get; set; } = 0m;
    public decimal CurrentDue { get; set; } = 0m;
}

public class Expense : TenantEntity
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDateUtc { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public Guid? RecordedByUserId { get; set; }
}

public enum SaleStatus
{
    Completed = 1,
    Returned = 2,
    Cancelled = 3
}

public class Sale : TenantEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? CustomerNameSnapshot { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal DueAmount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string IdempotencyKey { get; set; } = string.Empty;
    public SaleStatus Status { get; set; } = SaleStatus.Completed;
    public Guid? CashierUserId { get; set; }

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}

public class SaleItem : TenantEntity
{
    public Guid SaleId { get; set; }
    public Sale? Sale { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public string ProductNameSnapshot { get; set; } = string.Empty;
    public decimal UnitCostPrice { get; set; }
    public decimal UnitSellingPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal TotalPrice { get; set; }
}

public class AuditLog : TenantEntity
{
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
}
