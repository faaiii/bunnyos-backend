using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BunnyOS.Ai;
using BunnyOS.Application.DTOs;
using BunnyOS.Domain.Entities;
using BunnyOS.Domain.Financial;
using BunnyOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BunnyOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class SalesController : ControllerBase
{
    private readonly BunnyDbContext _db;
    public SalesController(BunnyDbContext db) => _db = db;

    [HttpPost]
    public async Task<IActionResult> CreateSale([FromBody] CreateSaleRequest req)
    {
        if (req.Items == null || req.Items.Count == 0)
            return BadRequest("বিক্রির জন্য অন্তত একটি পণ্য নির্বাচন করুন।");

        // Idempotency verification
        if (!string.IsNullOrWhiteSpace(req.IdempotencyKey))
        {
            var existing = await _db.Sales.Include(s => s.Items).FirstOrDefaultAsync(s => s.IdempotencyKey == req.IdempotencyKey);
            if (existing != null)
            {
                return Ok(existing);
            }
        }

        var productIds = req.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        var saleItems = new List<SaleItem>();
        decimal subtotal = 0m;

        foreach (var reqItem in req.Items)
        {
            if (!products.TryGetValue(reqItem.ProductId, out var product))
                return BadRequest($"পণ্য পাওয়া যায়নি: {reqItem.ProductId}");

            var unitPrice = reqItem.UnitPrice ?? product.SellingPrice;
            var lineTotal = FinancialCalculations.CalculateLineTotal(unitPrice, reqItem.Quantity);
            subtotal += lineTotal;

            // Deduct inventory
            product.StockQuantity -= reqItem.Quantity;

            saleItems.Add(new SaleItem
            {
                ProductId = product.Id,
                ProductNameSnapshot = product.Name,
                UnitCostPrice = product.CostPrice,
                UnitSellingPrice = unitPrice,
                Quantity = reqItem.Quantity,
                TotalPrice = lineTotal
            });
        }

        var (grandTotal, dueAmount) = FinancialCalculations.CalculateSaleTotals(
            subtotal, req.DiscountAmount, 0m, req.PaidAmount);

        var invoiceNum = "INV-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var sale = new Sale
        {
            InvoiceNumber = invoiceNum,
            CustomerId = req.CustomerId,
            CustomerNameSnapshot = req.CustomerName,
            Subtotal = subtotal,
            DiscountAmount = req.DiscountAmount,
            TaxAmount = 0m,
            TotalAmount = grandTotal,
            PaidAmount = req.PaidAmount,
            DueAmount = dueAmount,
            PaymentMethod = string.IsNullOrWhiteSpace(req.PaymentMethod) ? "Cash" : req.PaymentMethod,
            IdempotencyKey = string.IsNullOrWhiteSpace(req.IdempotencyKey) ? Guid.NewGuid().ToString() : req.IdempotencyKey,
            Status = SaleStatus.Completed,
            Items = saleItems
        };

        _db.Sales.Add(sale);

        // Update customer ledger if customer is linked
        if (req.CustomerId.HasValue)
        {
            var customer = await _db.Customers.FindAsync(req.CustomerId.Value);
            if (customer != null)
            {
                customer.TotalPurchases += grandTotal;
                customer.TotalPaid += req.PaidAmount;
                customer.CurrentDue += dueAmount;
            }
        }

        _db.PlatformAuditLogs.Add(new PlatformAuditLog
        {
            ActorEmail = "pos@system",
            ActorRole = "SalesPOS",
            Action = "SaleCompleted",
            TargetResource = sale.InvoiceNumber,
            Result = "Success",
            Details = $"POS Sale completed: Invoice #{sale.InvoiceNumber}, Total: ৳{sale.TotalAmount:N2}, Paid: ৳{sale.PaidAmount:N2}",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        await _db.SaveChangesAsync();

        return Ok(new
        {
            sale.Id,
            sale.InvoiceNumber,
            sale.Subtotal,
            sale.DiscountAmount,
            sale.TotalAmount,
            sale.PaidAmount,
            sale.DueAmount,
            sale.PaymentMethod,
            sale.CreatedAtUtc
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetSales([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var sales = await _db.Sales
            .Include(s => s.Items)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(sales);
    }
}

[ApiController]
[Route("api/v1/[controller]")]
public class HealthController : ControllerBase
{
    private readonly BunnyDbContext _db;
    public HealthController(BunnyDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> CheckHealth()
    {
        var canConnect = await _db.Database.CanConnectAsync();
        return Ok(new
        {
            status = "Healthy",
            app = "BunnyOS Native Backend",
            version = "2.0.0",
            databaseConnected = canConnect,
            timestampUtc = DateTime.UtcNow
        });
    }
}
