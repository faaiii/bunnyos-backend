using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BunnyOS.Ai.Tools;
using BunnyOS.Ai.Tools.Implementations;
using BunnyOS.Application.DTOs;
using BunnyOS.Application.Interfaces;

namespace BunnyOS.Ai;

public class AiToolRegistry
{
    private readonly Dictionary<string, IAiTool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public AiToolRegistry(IBunnyDbContext db)
    {
        Register(new GreetingTool());
        Register(new GetDashboardSummaryTool(db));
        Register(new GetLowStockProductsTool(db));
        Register(new SearchProductsTool(db));
        Register(new CreateExpenseTool(db));
        Register(new NavigateToScreenTool());
    }

    public void Register(IAiTool tool) => _tools[tool.Name] = tool;

    public IAiTool? GetTool(string name) => _tools.GetValueOrDefault(name);

    public IEnumerable<IAiTool> GetAllTools() => _tools.Values;
}

public class BanglaIntentClassifier
{
    public static string ClassifyIntent(string text)
    {
        var lower = text.ToLowerInvariant().Trim();

        // Greetings
        if (lower == "hi" || lower == "hello" || lower == "hey" || lower == "হাই" || lower == "হ্যালো" ||
            lower.Contains("সালাম") || lower.Contains("কেমন আছ") || lower.Contains("good morning"))
        {
            return "greeting";
        }

        // Dashboard & Sales inquiries
        if (lower.Contains("আজকের বিক্রি") || lower.Contains("আজকের সেলস") || lower.Contains("আজকের হিসাব") ||
            lower.Contains("আজকের লাভ") || lower.Contains("today sales") || lower.Contains("daily summary"))
        {
            return "get_dashboard_summary";
        }

        // Low stock inquiries
        if (lower.Contains("স্টক কমে") || lower.Contains("কম স্টক") || lower.Contains("স্টক শেষ") ||
            lower.Contains("low stock") || lower.Contains("out of stock") || lower.Contains("স্বল্প স্টক"))
        {
            return "get_low_stock_products";
        }

        // Expense recording
        if (lower.Contains("খরচ") || lower.Contains("expense") || lower.Contains("টাকা খরচ") || lower.Contains("বিল প্রদান"))
        {
            return "create_expense";
        }

        // Screen navigation
        if (lower.Contains("পেজ খুল") || lower.Contains("স্ক্রিনে যাও") || lower.Contains("ওপেন কর") ||
            lower.Contains("navigate") || lower.Contains("open screen") || lower.Contains("যাও"))
        {
            return "navigate_to_screen";
        }

        // Product search
        if (lower.Contains("পণ্য") || lower.Contains("খোঁজ") || lower.Contains("product") || lower.Contains("search"))
        {
            return "search_products";
        }

        // Default to dashboard summary
        return "get_dashboard_summary";
    }
}

public class BunnyAiEngine
{
    private readonly AiToolRegistry _toolRegistry;

    public BunnyAiEngine(AiToolRegistry toolRegistry)
    {
        _toolRegistry = toolRegistry;
    }

    public async Task<AiExecutionResult> ProcessCommandAsync(
        string command,
        bool isConfirmed = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return new AiExecutionResult(false, "unknown", "অনুগ্রহ করে আপনার ব্যবসায়িক নির্দেশ প্রদান করুন।", null, false, null, null);
        }

        var toolName = BanglaIntentClassifier.ClassifyIntent(command);
        var tool = _toolRegistry.GetTool(toolName);

        if (tool == null)
        {
            return new AiExecutionResult(
                false,
                "not_found",
                "দুঃখিত, এই মুহূর্তের জন্য অনুরোধটি সুরক্ষিত টুলে উপলব্ধ নয়।",
                null,
                false,
                null,
                null);
        }

        return await tool.ExecuteAsync(command, isConfirmed, cancellationToken);
    }
}
