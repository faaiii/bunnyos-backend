using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BunnyOS.Domain.Entities;
using BunnyOS.Domain.Financial;
using BunnyOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BunnyOS.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/reports")]
public class ReportsController : ControllerBase
{
    private readonly BunnyDbContext _db;

    public ReportsController(BunnyDbContext db)
    {
        _db = db;
    }

    [HttpGet("pnl")]
    [HttpGet("profit-loss")]
    public async Task<IActionResult> GetProfitAndLoss([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-30);
        var end = endDate ?? DateTime.UtcNow;

        var sales = await _db.Sales
            .Include(s => s.Items)
            .Where(s => s.CreatedAtUtc >= start && s.CreatedAtUtc <= end && s.Status == SaleStatus.Completed)
            .ToListAsync();

        var expenses = await _db.Expenses
            .Where(e => e.ExpenseDateUtc >= start && e.ExpenseDateUtc <= end)
            .ToListAsync();

        decimal grossSales = sales.Sum(s => s.Subtotal);
        decimal totalDiscounts = sales.Sum(s => s.DiscountAmount);
        decimal netSales = sales.Sum(s => s.TotalAmount);

        decimal cogs = sales.SelectMany(s => s.Items)
            .Sum(item => item.Quantity * item.UnitCostPrice);

        decimal grossProfit = netSales - cogs;
        decimal operatingExpenses = expenses.Sum(e => e.Amount);
        decimal netProfit = grossProfit - operatingExpenses;

        return Ok(new
        {
            period = new { start = start.ToString("yyyy-MM-dd"), end = end.ToString("yyyy-MM-dd") },
            financialSummary = new
            {
                grossSales,
                discounts = totalDiscounts,
                netSales,
                costOfGoodsSold = cogs,
                grossProfit,
                operatingExpenses,
                netProfit,
                grossMarginPercent = netSales > 0 ? Math.Round((grossProfit / netSales) * 100, 2) : 0,
                netMarginPercent = netSales > 0 ? Math.Round((netProfit / netSales) * 100, 2) : 0
            }
        });
    }

    [HttpGet("export-csv")]
    public async Task<IActionResult> ExportSalesCsv()
    {
        var sales = await _db.Sales.Include(s => s.Customer).OrderByDescending(s => s.CreatedAtUtc).ToListAsync();
        var sb = new StringBuilder();
        sb.AppendLine("InvoiceNumber,Customer,TotalAmount,PaidAmount,DueAmount,Status,Date");

        foreach (var s in sales)
        {
            var customer = s.Customer?.Name ?? s.CustomerNameSnapshot ?? "Walk-in";
            sb.AppendLine($"\"{s.InvoiceNumber}\",\"{customer}\",{s.TotalAmount},{s.PaidAmount},{s.DueAmount},{s.Status},\"{s.CreatedAtUtc:yyyy-MM-dd HH:mm}\"");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"bunnyos_sales_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var todaySales = await _db.Sales
            .Include(s => s.Items)
            .Where(s => s.CreatedAtUtc >= today && s.CreatedAtUtc < tomorrow && s.Status == SaleStatus.Completed)
            .ToListAsync();

        var todayExpenses = await _db.Expenses
            .Where(e => e.ExpenseDateUtc >= today && e.ExpenseDateUtc < tomorrow)
            .SumAsync(e => e.Amount);

        var salesTotal = todaySales.Sum(s => s.TotalAmount);
        var soldItems = todaySales.SelectMany(s => s.Items)
            .Select(i => (i.UnitCostPrice, i.UnitSellingPrice, i.Quantity));

        var grossProfit = FinancialCalculations.CalculateGrossProfit(soldItems);
        var netProfit = FinancialCalculations.CalculateNetProfit(grossProfit, todayExpenses);

        var totalCustomerDues = await _db.Customers.SumAsync(c => c.CurrentDue);
        var totalSupplierDues = await _db.Suppliers.SumAsync(s => s.CurrentDue);

        var lowStockCount = await _db.Products
            .CountAsync(p => p.IsActive && p.StockQuantity <= p.LowStockThreshold);

        var recentSales = await _db.Sales
            .Include(s => s.Items)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Take(5)
            .Select(s => new BunnyOS.Application.DTOs.SaleDto(
                s.Id,
                s.InvoiceNumber,
                s.CustomerId,
                s.CustomerNameSnapshot,
                s.Subtotal,
                s.DiscountAmount,
                s.TaxAmount,
                s.TotalAmount,
                s.PaidAmount,
                s.DueAmount,
                s.PaymentMethod,
                s.Status.ToString(),
                s.CreatedAtUtc,
                s.Items.Select(i => new BunnyOS.Application.DTOs.SaleItemDto(i.ProductId ?? Guid.Empty, i.ProductNameSnapshot, i.UnitCostPrice, i.UnitSellingPrice, i.Quantity, i.TotalPrice)).ToList()
            )).ToListAsync();

        var lowStockProducts = await _db.Products
            .Where(p => p.IsActive && p.StockQuantity <= p.LowStockThreshold)
            .Take(5)
            .Select(p => new BunnyOS.Application.DTOs.ProductDto(p.Id, p.Name, p.Sku, p.CategoryId, null, p.CostPrice, p.SellingPrice, p.StockQuantity, p.LowStockThreshold, p.Unit, true))
            .ToListAsync();

        var dto = new BunnyOS.Application.DTOs.DashboardSummaryDto(
            salesTotal,
            todayExpenses,
            grossProfit,
            netProfit,
            totalCustomerDues,
            totalSupplierDues,
            lowStockCount,
            recentSales,
            lowStockProducts,
            $"আজকে বিক্রয় হয়েছে ৳ {salesTotal:N2}। নেট প্রফিট ৳ {netProfit:N2}।"
        );

        return Ok(dto);
    }
}
