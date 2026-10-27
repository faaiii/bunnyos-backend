using System;
using System.Collections.Generic;
using BunnyOS.Domain.Common;

namespace BunnyOS.Domain.Entities;

public class Category : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Product : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal StockQuantity { get; set; }
    public decimal LowStockThreshold { get; set; } = 5m;
    public string Unit { get; set; } = "Pcs";
    public bool IsActive { get; set; } = true;

    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}

public enum StockMovementType
{
    InitialStock = 1,
    SaleDeduction = 2,
    PurchaseRestock = 3,
    ManualAdjustment = 4,
    ReturnRestock = 5,
    DamageLoss = 6
}

public class StockMovement : TenantEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal QuantityChange { get; set; }
    public decimal PreviousQuantity { get; set; }
    public decimal NewQuantity { get; set; }
    public StockMovementType MovementType { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public Guid? ReferenceId { get; set; } // SaleId or PurchaseId
}
