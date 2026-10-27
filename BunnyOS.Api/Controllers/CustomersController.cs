using System;
using System.Linq;
using System.Threading.Tasks;
using BunnyOS.Application.DTOs;
using BunnyOS.Domain.Entities;
using BunnyOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BunnyOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly BunnyDbContext _db;
    public CustomersController(BunnyDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetCustomers([FromQuery] string? search)
    {
        var query = _db.Customers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c => c.Name.Contains(search) || c.Phone.Contains(search));
        }

        var list = await query
            .OrderBy(c => c.Name)
            .Select(c => new CustomerDto(
                c.Id,
                c.Name,
                c.Phone,
                c.Email,
                c.Address,
                c.TotalPurchases,
                c.TotalPaid,
                c.CurrentDue
            ))
            .ToListAsync();

        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Phone))
            return BadRequest("গ্রাহকের নাম এবং মোবাইল নম্বর আবশ্যক।");

        var customer = new Customer
        {
            Name = req.Name.Trim(),
            Phone = req.Phone.Trim(),
            Email = req.Email?.Trim(),
            Address = req.Address?.Trim(),
            TotalPurchases = 0m,
            TotalPaid = 0m,
            CurrentDue = 0m
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var dto = new CustomerDto(
            customer.Id,
            customer.Name,
            customer.Phone,
            customer.Email,
            customer.Address,
            customer.TotalPurchases,
            customer.TotalPaid,
            customer.CurrentDue
        );

        return CreatedAtAction(nameof(GetCustomers), new { id = customer.Id }, dto);
    }
}
