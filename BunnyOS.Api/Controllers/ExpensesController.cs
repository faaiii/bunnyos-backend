using System;
using System.Linq;
using System.Threading.Tasks;
using BunnyOS.Domain.Entities;
using BunnyOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BunnyOS.Api.Controllers;

[ApiController]
[Route("api/v1/expenses")]
public class ExpensesController : ControllerBase
{
    private readonly BunnyDbContext _db;

    public ExpensesController(BunnyDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var expenses = await _db.Expenses
            .OrderByDescending(e => e.ExpenseDateUtc)
            .Take(100)
            .Select(e => new
            {
                id = e.Id,
                category = e.Category,
                amount = e.Amount,
                date = e.ExpenseDateUtc.ToString("MMM dd, yyyy"),
                paymentMethod = e.PaymentMethod,
                note = e.Note
            })
            .ToListAsync();

        var total = expenses.Sum(e => e.amount);
        return Ok(new { total, items = expenses });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExpenseRequest req)
    {
        if (req.Amount <= 0)
            return BadRequest("খরচের পরিমাণ ০ এর বেশি হতে হবে।");

        var expense = new Expense
        {
            Category = string.IsNullOrWhiteSpace(req.Category) ? "General" : req.Category,
            Amount = req.Amount,
            PaymentMethod = string.IsNullOrWhiteSpace(req.PaymentMethod) ? "Cash" : req.PaymentMethod,
            Note = req.Note,
            ExpenseDateUtc = req.Date ?? DateTime.UtcNow
        };

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();

        return Ok(new { success = true, id = expense.Id, message = "খরচ সফলভাবে রেকর্ড করা হয়েছে।" });
    }
}

public record CreateExpenseRequest(string Category, decimal Amount, string PaymentMethod, string? Note, DateTime? Date);
