using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BunnyOS.Application.DTOs;
using BunnyOS.Application.Interfaces;
using BunnyOS.Domain.Entities;
using BunnyOS.Domain.Financial;
using Microsoft.EntityFrameworkCore;

namespace BunnyOS.Ai.Tools.Implementations;

public class GreetingTool : IAiTool
{
    public string Name => "greeting";
    public string Description => "Handles friendly greetings and introduces Bunny AI capabilities.";
    public string DescriptionBangla => "বন্ধুত্বপূর্ণ অভিবাদন ও সহায়তার প্রস্তাব দেয়।";
    public bool RequiresConfirmation => false;

    public Task<AiExecutionResult> ExecuteAsync(string command, bool isConfirmed, CancellationToken cancellationToken = default)
    {
        var msg = "হ্যালো! আমি আপনার Bunny AI সহকারী। আপনার ব্যবসার আজকের বিক্রি, লাভ-ক্ষতি, বর্তমান স্টক বা যেকোনো খরচ এন্ট্রি করতে আমাকে নির্দেশ দিন।";
        return Task.FromResult(new AiExecutionResult(true, Name, msg, null, false, null, "dashboard"));
    }
}

public class GetDashboardSummaryTool : IAiTool
{
    private readonly IBunnyDbContext _db;
    public GetDashboardSummaryTool(IBunnyDbContext db) => _db = db;

    public string Name => "get_dashboard_summary";
    public string Description => "Retrieves today's sales, expenses, gross & net profit, and critical business metrics.";
    public string DescriptionBangla => "আজকের মোট বিক্রি, খরচ, লাভ এবং ব্যবসার সারাংশ প্রদান করে।";
    public bool RequiresConfirmation => false;

    public async Task<AiExecutionResult> ExecuteAsync(string command, bool isConfirmed, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var sales = await _db.Sales
            .Include(s => s.Items)
            .Where(s => s.CreatedAtUtc >= today && s.CreatedAtUtc < tomorrow && s.Status == SaleStatus.Completed)
            .ToListAsync(cancellationToken);

        var expenses = await _db.Expenses
            .Where(e => e.ExpenseDateUtc >= today && e.ExpenseDateUtc < tomorrow)
            .ToListAsync(cancellationToken);

        var todaySalesTotal = sales.Sum(s => s.TotalAmount);
        var todayExpensesTotal = expenses.Sum(e => e.Amount);

        var soldItems = sales.SelectMany(s => s.Items)
            .Select(i => (i.UnitCostPrice, i.UnitSellingPrice, i.Quantity));

        var grossProfit = FinancialCalculations.CalculateGrossProfit(soldItems);
        var netProfit = FinancialCalculations.CalculateNetProfit(grossProfit, todayExpensesTotal);

        var totalCustomerDues = await _db.Customers.SumAsync(c => c.CurrentDue, cancellationToken);
        var totalSupplierDues = await _db.Suppliers.SumAsync(s => s.CurrentDue, cancellationToken);
        var lowStockCount = await _db.Products.CountAsync(p => p.IsActive && p.StockQuantity <= p.LowStockThreshold, cancellationToken);

        var summary = new DashboardSummaryDto(
            todaySalesTotal,
            todayExpensesTotal,
            grossProfit,
            netProfit,
            totalCustomerDues,
            totalSupplierDues,
            lowStockCount,
            new List<SaleDto>(),
            new List<ProductDto>(),
            $"আজকে মোট বিক্রি ৳ {todaySalesTotal:N2}, মোট লাভ ৳ {netProfit:N2}।");

        return new AiExecutionResult(
            true,
            Name,
            $"আজকের বিক্রি: ৳ {todaySalesTotal:N2} | খরচ: ৳ {todayExpensesTotal:N2} | মোট লাভ: ৳ {grossProfit:N2} | নেট প্রফিট: ৳ {netProfit:N2}",
            summary,
            false,
            null,
            "dashboard");
    }
}

public class GetLowStockProductsTool : IAiTool
{
    private readonly IBunnyDbContext _db;
    public GetLowStockProductsTool(IBunnyDbContext db) => _db = db;

    public string Name => "get_low_stock_products";
    public string Description => "Lists all products whose stock is at or below their low stock threshold.";
    public string DescriptionBangla => "যেসব পণ্যের স্টক ফুরিয়ে আসছে বা নির্ধারিত সীমার নিচে নেমে গেছে তাদের তালিকা দেখায়।";
    public bool RequiresConfirmation => false;

    public async Task<AiExecutionResult> ExecuteAsync(string command, bool isConfirmed, CancellationToken cancellationToken = default)
    {
        var lowStock = await _db.Products
            .Where(p => p.IsActive && p.StockQuantity <= p.LowStockThreshold)
            .Select(p => new ProductDto(p.Id, p.Name, p.Sku, p.CategoryId, null, p.CostPrice, p.SellingPrice, p.StockQuantity, p.LowStockThreshold, p.Unit, true))
            .ToListAsync(cancellationToken);

        var msg = lowStock.Count == 0
            ? "আলহামদুলিল্লাহ! বর্তমানে কোনো পণ্যের স্টক সংকট নেই।"
            : $"সতর্কতা: {lowStock.Count} টি পণ্যের স্টক কমে গেছে: {string.Join(", ", lowStock.Take(3).Select(p => p.Name))}";

        return new AiExecutionResult(true, Name, msg, lowStock, false, null, "inventory");
    }
}

public class SearchProductsTool : IAiTool
{
    private readonly IBunnyDbContext _db;
    public SearchProductsTool(IBunnyDbContext db) => _db = db;

    public string Name => "search_products";
    public string Description => "Searches products by name, SKU or category.";
    public string DescriptionBangla => "পণ্যের নাম বা কোড দিয়ে ইনভেন্টরি সার্চ করে।";
    public bool RequiresConfirmation => false;

    public async Task<AiExecutionResult> ExecuteAsync(string command, bool isConfirmed, CancellationToken cancellationToken = default)
    {
        var query = command.Trim();
        var products = await _db.Products
            .Where(p => p.IsActive && (p.Name.Contains(query) || p.Sku.Contains(query)))
            .Take(10)
            .Select(p => new ProductDto(p.Id, p.Name, p.Sku, p.CategoryId, null, p.CostPrice, p.SellingPrice, p.StockQuantity, p.LowStockThreshold, p.Unit, p.StockQuantity <= p.LowStockThreshold))
            .ToListAsync(cancellationToken);

        return new AiExecutionResult(true, Name, $"খুঁজে পাওয়া গেছে {products.Count} টি পণ্য।", products, false, null, "inventory");
    }
}

public class CreateExpenseTool : IAiTool
{
    private readonly IBunnyDbContext _db;
    public CreateExpenseTool(IBunnyDbContext db) => _db = db;

    public string Name => "create_expense";
    public string Description => "Records an operating expense (Rent, Tea, Transport, Utilities, etc.)";
    public string DescriptionBangla => "দোকান বা অফিসের যেকোনো খরচ রেকর্ড করে।";
    public bool RequiresConfirmation => true;

    public async Task<AiExecutionResult> ExecuteAsync(string command, bool isConfirmed, CancellationToken cancellationToken = default)
    {
        decimal amount = 0m;
        var normalized = BunnyAiUtils.ConvertBanglaDigitsToEnglish(command);
        var words = normalized.Split(' ');
        foreach (var w in words)
        {
            var clean = new string(w.Where(char.IsDigit).ToArray());
            if (decimal.TryParse(clean, out var val) && val > 0)
            {
                amount = val;
                break;
            }
        }

        if (amount <= 0) amount = 100m; // Fallback sensible default if extracted

        if (!isConfirmed)
        {
            return new AiExecutionResult(
                false,
                Name,
                $"আপনি কি নিশ্চিত যে ৳ {amount:N2} টাকা খরচ হিসেবে যোগ করতে চান?",
                null,
                true,
                $"খরচ অনুমোদন: ৳ {amount:N2} যোগ করা হবে।",
                "expenses");
        }

        var expense = new Expense
        {
            Category = "অফিস / বিবিধ খরচ",
            Amount = amount,
            ExpenseDateUtc = DateTime.UtcNow,
            PaymentMethod = "Cash",
            Note = "Recorded via Bunny AI: " + command
        };

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync(cancellationToken);

        return new AiExecutionResult(
            true,
            Name,
            $"সাফল্যের সাথে ৳ {amount:N2} টাকা খরচ তালিকায় যোগ করা হয়েছে।",
            expense,
            false,
            null,
            "expenses");
    }
}

public class NavigateToScreenTool : IAiTool
{
    public string Name => "navigate_to_screen";
    public string Description => "Navigates the user interface to a specified screen.";
    public string DescriptionBangla => "নির্দিষ্ট স্ক্রিনে নিয়ে যায় (যেমন: সেলস, ইনভেন্টরি, রিপোর্ট)।";
    public bool RequiresConfirmation => false;

    public Task<AiExecutionResult> ExecuteAsync(string command, bool isConfirmed, CancellationToken cancellationToken = default)
    {
        string target = "dashboard";
        var lower = command.ToLowerInvariant();
        if (lower.Contains("বিক্রি") || lower.Contains("sales") || lower.Contains("pos")) target = "sales";
        else if (lower.Contains("স্টক") || lower.Contains("ইনভেন্টরি") || lower.Contains("product") || lower.Contains("inventory")) target = "inventory";
        else if (lower.Contains("রিপোর্ট") || lower.Contains("report")) target = "reports";
        else if (lower.Contains("খরচ") || lower.Contains("expense")) target = "expenses";
        else if (lower.Contains("কাস্টমার") || lower.Contains("customer")) target = "customers";
        else if (lower.Contains("সেটিংস") || lower.Contains("settings")) target = "settings";

        return Task.FromResult(new AiExecutionResult(true, Name, $"স্ক্রিনে নিয়ে যাওয়া হচ্ছে: {target}", null, false, null, target));
    }
}

public static class BunnyAiUtils
{
    public static string ConvertBanglaDigitsToEnglish(string input)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var ch in input)
        {
            if (ch >= '০' && ch <= '৯')
                sb.Append((char)('0' + (ch - '০')));
            else
                sb.Append(ch);
        }
        return sb.ToString();
    }
}


