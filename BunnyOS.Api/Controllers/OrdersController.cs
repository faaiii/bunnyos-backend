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
}
