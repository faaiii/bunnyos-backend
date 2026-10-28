using System;
using System.Linq;
using System.Threading.Tasks;
using BunnyOS.Domain.Entities;
using BunnyOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BunnyOS.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
public class OrdersController : ControllerBase
{
    private readonly BunnyDbContext _db;

    public OrdersController(BunnyDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders([FromQuery] string? status, [FromQuery] string? search)
    {
        var query = _db.Sales.Include(s => s.Customer).Include(s => s.Items).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(o => o.InvoiceNumber.ToLower().Contains(s) || (o.Customer != null && o.Customer.Name.ToLower().Contains(s)));
        }

        var sales = await query.OrderByDescending(s => s.CreatedAtUtc).Take(50).ToListAsync();

        var orders = sales.Select(s => new
        {
            id = s.Id,
            orderId = s.InvoiceNumber,
            customer = s.Customer != null ? s.Customer.Name : (s.CustomerNameSnapshot ?? "Walk-in Customer"),
            total = s.TotalAmount,
            status = s.Status == SaleStatus.Completed ? "Delivered" : "Processing",
            date = s.CreatedAtUtc.ToString("MMM dd, yyyy"),
            itemCount = s.Items.Count
        });

        return Ok(orders);
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] OrderCreateDto req)
    {
        if (req == null)
            return BadRequest("অর্ডারের তথ্য প্রদান করুন।");

        if (req.Items == null || req.Items.Count == 0)
        {
            if (req.TotalAmount > 0)
            {
                req.Items = new List<OrderItemDto>
                {
                    new() { Name = "দ্রুত বিক্রয় (Quick Sale Item)", Quantity = 1, Price = req.TotalAmount }
                };
            }
            else
            {
                return BadRequest("অর্ডারের পণ্য বিবরণ প্রদান করুন।");
            }
        }

        var invoiceNum = "INV-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var saleItems = new List<SaleItem>();
        decimal computedSubtotal = 0m;

        foreach (var item in req.Items)
        {
            var pName = string.IsNullOrWhiteSpace(item.Name) ? "সাধারণ পণ্য" : item.Name.Trim();
            var qty = item.Quantity <= 0 ? 1 : item.Quantity;
            var price = item.Price <= 0 ? 0 : item.Price;
            var lineTotal = price * qty;
            computedSubtotal += lineTotal;

            Guid? prodId = null;
            if (!string.IsNullOrWhiteSpace(item.ProductId) && Guid.TryParse(item.ProductId, out var parsedId))
            {
                prodId = parsedId;
                var existingProd = await _db.Products.FindAsync(parsedId);
                if (existingProd != null)
                {
                    existingProd.StockQuantity = Math.Max(0, existingProd.StockQuantity - qty);
                }
            }

            saleItems.Add(new SaleItem
            {
                ProductId = prodId,
                ProductNameSnapshot = pName,
                UnitCostPrice = price * 0.7m,
                UnitSellingPrice = price,
                Quantity = qty,
                TotalPrice = lineTotal
            });
        }

        var finalTotal = req.TotalAmount > 0 ? req.TotalAmount : computedSubtotal;

        var sale = new Sale
        {
            InvoiceNumber = invoiceNum,
            CustomerNameSnapshot = string.IsNullOrWhiteSpace(req.CustomerName) ? "Walk-in Customer" : req.CustomerName.Trim(),
            Subtotal = computedSubtotal,
            DiscountAmount = 0m,
            TaxAmount = 0m,
            TotalAmount = finalTotal,
            PaidAmount = finalTotal,
            DueAmount = 0m,
            PaymentMethod = string.IsNullOrWhiteSpace(req.PaymentMethod) ? "Cash" : req.PaymentMethod,
            IdempotencyKey = Guid.NewGuid().ToString(),
            Status = SaleStatus.Completed,
            Items = saleItems
        };

        _db.Sales.Add(sale);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            id = sale.Id,
            orderId = sale.InvoiceNumber,
            total = sale.TotalAmount,
            status = "Completed",
            message = "বিক্রয় ও অর্ডার সফলভাবে সম্পন্ন হয়েছে।"
        });
    }
}

public class OrderCreateDto
{
    public string? CustomerName { get; set; }
    public string? PaymentMethod { get; set; }
    public decimal TotalAmount { get; set; }
    public List<OrderItemDto>? Items { get; set; }
}

public class OrderItemDto
{
    public string? ProductId { get; set; }
    public string? Name { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
}
