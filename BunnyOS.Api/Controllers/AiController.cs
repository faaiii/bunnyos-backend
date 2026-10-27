using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BunnyOS.Ai;
using BunnyOS.Application.DTOs;
using BunnyOS.Domain.Entities;
using BunnyOS.Domain.Financial;
using BunnyOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BunnyOS.Api.Controllers;

[ApiController]
[Route("api/v1/ai")]
public class AiController : ControllerBase
{
    private readonly BunnyDbContext _db;
    private readonly BunnyAiEngine _aiEngine;
    private readonly IConfiguration _config;
    private static readonly HttpClient _httpClient = new(new SocketsHttpHandler
    {
        ConnectCallback = async (context, cancellationToken) =>
        {
            var entry = await System.Net.Dns.GetHostEntryAsync(context.DnsEndPoint.Host, System.Net.Sockets.AddressFamily.InterNetwork, cancellationToken);
            var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            await socket.ConnectAsync(entry.AddressList[0], context.DnsEndPoint.Port, cancellationToken);
            return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
        }
    }) { Timeout = TimeSpan.FromSeconds(90) };

    public AiController(BunnyDbContext db, BunnyAiEngine aiEngine, IConfiguration config)
    {
        _db = db;
        _aiEngine = aiEngine;
        _config = config;
    }

    private string? GetGeminiApiKey()
    {
        var key = _config["GeminiApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (!string.IsNullOrWhiteSpace(key)) return key;

        try
        {
            using var cmd = _db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "SELECT \"Value\" FROM \"PlatformSettings\" WHERE \"Key\" = 'GeminiApiKey' LIMIT 1;";
            if (cmd.Connection.State != System.Data.ConnectionState.Open) cmd.Connection.Open();
            var val = cmd.ExecuteScalar();
            if (val != null && !string.IsNullOrWhiteSpace(val.ToString()))
            {
                return val.ToString();
            }
        }
        catch { }

        return null;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            status = "Active",
            engine = "BunnyAiEngine + GeminiLLM",
            banglaSupported = true,
            hasGeminiKey = !string.IsNullOrWhiteSpace(GetGeminiApiKey()),
            timestamp = DateTime.UtcNow
        });
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] AiChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Prompt))
        {
            return BadRequest(new { response = "অনুগ্রহ করে আপনার প্রশ্ন লিখুন।", reply = "অনুগ্রহ করে আপনার প্রশ্ন লিখুন।", success = false });
        }

        var query = request.Prompt.Trim();
        var q = query.ToLowerInvariant();

        // ================================================================
        // 1. REAL DATABASE: Profit & Loss Inquiries
        // ================================================================
        if (q.Contains("আজকের লাভ") || q.Contains("আজকের প্রফিট") || q.Contains("আজকের হিসাব") || q.Contains("নেট লাভ") || q.Contains("লাভ কত") || q.Contains("লাভ-ক্ষতি") || q.Contains("profit & loss") || q.Contains("p&l"))
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
            var cogs = sales.SelectMany(s => s.Items).Sum(i => i.Quantity * i.UnitCostPrice);
            var grossProfit = todaySalesTotal - cogs;
            var netProfit = grossProfit - todayExpensesTotal;

            string text;
            if (sales.Count == 0 && todayExpensesTotal == 0)
            {
                text = "📊 **আজকের আর্থিক হিসাব (রিয়েল-টাইম ক্লাউড ডাটা):**\n\n" +
                       "• **মোট বিক্রি (Net Sales):** ৳ ০.০০\n" +
                       "• **বিক্রীত পণ্যের আসল খরচ (COGS):** ৳ ০.০০\n" +
                       "• **গ্রস প্রফিট:** ৳ ০.০০\n" +
                       "• **অপারেটিং খরচ:** ৳ ০.০০\n" +
                       "• **আজকের নিখুঁত নেট লাভ:** ৳ ০.০০\n\n" +
                       "💡 আজকের জন্য এখনো কোনো নতুন বিক্রি রেকর্ড করা হয়নি। পিওএস বা অর্ডার স্ক্রিন থেকে বিক্রি এন্ট্রি করলেই সাথে সাথে রিয়েল-টাইম লাভ-ক্ষতি এখানে দেখতে পাবেন!";
            }
            else
            {
                var margin = todaySalesTotal > 0 ? (grossProfit / todaySalesTotal * 100) : 0;
                text = $"📊 **আজকের আর্থিক হিসাব (লাইভ ক্লাউড ডাটাবেজ):**\n\n" +
                       $"• **মোট বিক্রি (Net Sales):** ৳ {todaySalesTotal:N2}\n" +
                       $"• **বিক্রীত পণ্যের খরচ (COGS):** ৳ {cogs:N2}\n" +
                       $"• **মোট গ্রস প্রফিট:** ৳ {grossProfit:N2} ({margin:F1}% মার্জিন)\n" +
                       $"• **অপারেটিং খরচ:** ৳ {todayExpensesTotal:N2}\n" +
                       $"• **আজকের নিখুঁত নেট লাভ:** ৳ {netProfit:N2}\n\n" +
                       "💡 সব হিসাব আপনার Supabase ক্লাউড ডাটাবেজ থেকে সরাসরি গণনা করা হয়েছে।";
            }

            return Ok(new { response = text, reply = text, success = true, timestamp = DateTime.UtcNow });
        }

        // ================================================================
        // 2. REAL DATABASE: Low Stock Inquiries
        // ================================================================
        if (q.Contains("লো স্টক") || q.Contains("লো-স্টক") || q.Contains("ঘাটতি পণ্য") || q.Contains("মজুদ কত") || q.Contains("স্টক কত") || q.Contains("low stock"))
        {
            var lowStock = await _db.Products
                .Where(p => p.IsActive && p.StockQuantity <= p.LowStockThreshold)
                .Take(10)
                .ToListAsync(cancellationToken);

            string text;
            if (lowStock.Count == 0)
            {
                text = "✅ **স্টক স্ট্যাটাস (রিয়েল-টাইম ইনভেন্টরি):**\n\n" +
                       "আলহামদুলিল্লাহ! আপনার দোকানে বর্তমানে কোনো পণ্যের ঘাটতি বা স্টক সংকট নেই। সব পণ্যের পর্যাপ্ত মজুদ রয়েছে।";
            }
            else
            {
                var items = string.Join("\n", lowStock.Select((p, idx) => $"{idx + 1}. **{p.Name}** — {p.StockQuantity:0.##} {p.Unit} বাকি (সতর্কতা সীমা: {p.LowStockThreshold:0.##} {p.Unit})"));
                text = $"⚠️ **লো-স্টক পণ্য তালিকা ({lowStock.Count} টি পণ্য):**\n\n" +
                       items +
                       "\n\n📦 সাপ্লায়ারকে দ্রুত নতুন অর্ডার দিয়ে স্টক পূরণ করার পরামর্শ দেওয়া হলো।";
            }

            return Ok(new { response = text, reply = text, success = true, timestamp = DateTime.UtcNow });
        }

        // ================================================================
        // 3. REAL DATABASE: Top / Best Selling Products
        // ================================================================
        if (q.Contains("শীর্ষ বিক্রিত") || q.Contains("বেশি বিক্রি হওয়া পণ্য") || q.Contains("সেরা বিক্রিত") || q.Contains("জনপ্রিয় পণ্য") || q.Contains("top selling") || q.Contains("top products"))
        {
            var topSales = await _db.SaleItems
                .GroupBy(i => i.ProductNameSnapshot)
                .Select(g => new { Name = g.Key, Quantity = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.TotalPrice) })
                .OrderByDescending(x => x.Quantity)
                .Take(5)
                .ToListAsync(cancellationToken);

            string text;
            if (topSales.Count == 0)
            {
                text = "🏆 **শীর্ষ বিক্রিত পণ্য:**\n\n" +
                       "আজকে এখনো কোনো পণ্য বিক্রি সম্পন্ন হয়নি। আপনি পিওএস স্ক্রিন থেকে বিক্রি শুরু করলেই শীর্ষ বিক্রিত পণ্যের তালিকা ও আয় স্বয়ংক্রিয়ভাবে এখানে চলে আসবে।";
            }
            else
            {
                var items = string.Join("\n", topSales.Select((p, idx) => $"{idx + 1}. **{p.Name}** ({p.Quantity} টি বিক্রি • ৳ {p.Revenue:N2})"));
                text = "🏆 **শীর্ষ বিক্রিত পণ্য তালিকা:**\n\n" + items + "\n\n💡 এই পণ্যগুলোর স্টক সবসময় পর্যাপ্ত রাখুন।";
            }

            return Ok(new { response = text, reply = text, success = true, timestamp = DateTime.UtcNow });
        }

        // ================================================================
        // 4. Operating Expenses & Cost Reduction
        // ================================================================
        if (q.Contains("অপারেটিং খরচ") || q.Contains("দোকানের খরচ কমানো") || q.Contains("ব্যবসায়িক খরচ কমানো") || q.Contains("operating cost") || q.Contains("reduce expense"))
        {
            string text = "💡 **অপারেটিং খরচ কমানোর কার্যকরী কৌশল:**\n\n" +
                          "১. **বাল্ক পারচেজিং ও সাপ্লায়ার ডিসকাউন্ট:** নিয়মিত দ্রুত বিক্রিত পণ্য একসাথে ক্রয় করে ৫-৮% পর্যন্ত খরচ সাশ্রয় করুন।\n" +
                          "২. **ইনভেন্টরি ডেড-স্টক প্রিভেনশন:** যেসব পণ্যের টার্নওভার কম, সেগুলোতে ক্যাশ আটকে না রেখে বান্ডেল অফারে ক্লিয়ার করুন।\n" +
                          "৩. **ডিজিটাল ইনভয়েসিং ও ক্যাশলেস পে:** পেপার ও প্রিন্টিং খরচ কমিয়ে সরাসরি এসএমএস/হোয়াটসঅ্যাপ রসিদ ব্যবহার করুন।\n" +
                          "৪. **ফিক্সড বিল অপ্টিমাইজেশন:** দোকান বা গুদামের বিদ্যুৎ ও অপ্রয়োজনীয় পেমেন্ট ফি নিয়মিত অডিট করুন।";

            return Ok(new { response = text, reply = text, success = true, timestamp = DateTime.UtcNow });
        }

        // ================================================================
        // 5. GENERAL AI: Google Gemini LLM for ANY Open-Ended Question
        // ================================================================
        var geminiApiKey = GetGeminiApiKey();
        if (!string.IsNullOrWhiteSpace(geminiApiKey))
        {
            var models = new[] { "gemini-flash-lite-latest", "gemini-flash-latest", "gemini-3.5-flash-lite" };
            var systemPrompt = "You are Bunny AI (বানি এআই), the highly intelligent, friendly AI assistant for BunnyOS and retail businesses in Bangladesh. " +
                               "Answer the user's question clearly, thoroughly, politely, and accurately with clean markdown formatting. " +
                               "If asked in Bengali or Banglish, reply in fluent, natural Bengali. If asked in English, reply in English. " +
                               "You can answer ANY question: business strategy, sales growth, accounting, general knowledge, science, mathematics, coding, or creative writing.";

            var reqPayload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = $"{systemPrompt}\n\nUser Question: {query}" }
                        }
                    }
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(reqPayload);

            foreach (var model in models)
            {
                try
                {
                    var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={geminiApiKey}";
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                    var res = await _httpClient.PostAsync(url, content, cts.Token);
                    if (res.IsSuccessStatusCode)
                    {
                        var resJson = await res.Content.ReadAsStringAsync(cts.Token);
                        using var doc = System.Text.Json.JsonDocument.Parse(resJson);
                        if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                        {
                            var partsEl = candidates[0].GetProperty("content").GetProperty("parts");
                            var sb = new StringBuilder();
                            foreach (var p in partsEl.EnumerateArray())
                            {
                                if (p.TryGetProperty("text", out var tEl))
                                {
                                    var txt = tEl.GetString();
                                    if (!string.IsNullOrWhiteSpace(txt)) sb.Append(txt);
                                }
                            }

                            var candidateText = sb.ToString();
                            if (!string.IsNullOrWhiteSpace(candidateText))
                            {
                                return Ok(new
                                {
                                    response = candidateText.Trim(),
                                    reply = candidateText.Trim(),
                                    modelUsed = model,
                                    success = true,
                                    timestamp = DateTime.UtcNow
                                });
                            }
                        }
                    }
                    else
                    {
                        var err = await res.Content.ReadAsStringAsync();
                        Console.WriteLine($"[Gemini API - {model}] HTTP {res.StatusCode}: {err}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Gemini API - {model}] {ex.Message}");
                }
            }
        }

        // ================================================================
        // 6. Intelligent Open Knowledge Fallback (Marketing, Customer, Advice)
        // ================================================================
        if (q.Contains("কাস্টমার") || q.Contains("গ্রাহক") || q.Contains("বিক্রি বাড়া") || q.Contains("customer"))
        {
            string reply = "🚀 **দোকানে কাস্টমার ও বিক্রি বাড়ানোর ৫টি কার্যকরী কৌশল:**\n\n" +
                           "১. **লোয়ালটি ও ক্যাশব্যাক অফার:** নিয়মিত ক্রেতাদের জন্য প্রতি ৫০০ টাকার কেনাকাটায় পয়েন্ট বা ডিসকাউন্ট দিন।\n" +
                           "২. **হোয়াটসঅ্যাপ ও এসএমএস মার্কেটিং:** নতুন পণ্য আসা মাত্রই আপনার রেগুলার কাস্টমারদের জানিয়ে দিন।\n" +
                           "৩. **পণ্য প্রদর্শন ও পরিষ্কার-পরিচ্ছন্নতা:** দোকানের সামনের অংশে ট্রেন্ডিং ও নিত্যপ্রয়োজনীয় মালামাল আকর্ষণীয়ভাবে সাজিয়ে রাখুন।\n" +
                           "৪. **দ্রুত চেকআউট ও পেমেন্ট:** বিকাশ, নগদ বা কার্ড পেমেন্টের সুবিধা রাখুন যাতে কাউন্টারে ভিড় না জমে।\n" +
                           "৫. **বিনয়ী ব্যবহার ও ভালো আফটার-সেলস:** কাস্টমারের সাথে হাসিমুখে কথা বলুন এবং পণ্যের সঠিক গুণগত মান বজায় রাখুন।";
            return Ok(new { response = reply, reply = reply, success = true, timestamp = DateTime.UtcNow });
        }

        if (q.Contains("অফার") || q.Contains("মেসেজ") || q.Contains("এসএমএস") || q.Contains("sms") || q.Contains("offer"))
        {
            string reply = "📩 **কাস্টমারদের জন্য আকর্ষণীয় অফার এসএমএস ড্রাফট:**\n\n" +
                           "**অপশন ১ (সাধারণ অফার):**\n" +
                           "\"প্রিয় গ্রাহক, BENE স্টোরে চলছে বিশেষ কেনাকাটা উৎসব! নির্দিষ্ট পণ্যে পাচ্ছেন আকর্ষণীয় ছাড়। আজই চলে আসুন আমাদের শপে। হটলাইন: 01740420545\"\n\n" +
                           "**অপশন ২ (উইকএন্ড অফার):**\n" +
                           "\"ছুটির দিনে BENE স্টোর থেকে ১,০০০ টাকার কেনাকাটায় পাচ্ছেন স্পেশাল গিফট! অফারটি সীমিত সময়ের জন্য।\"\n\n" +
                           "💡 আপনি চাইলে আপনার সুবিধামতো পণ্যের নাম যুক্ত করে এই মেসেজ পাঠাতে পারেন।";
            return Ok(new { response = reply, reply = reply, success = true, timestamp = DateTime.UtcNow });
        }

        if (q.Contains("বাকি") || q.Contains("টাকা আদায়") || q.Contains("due"))
        {
            string reply = "📲 **বকেয়া টাকা আদায়ের জন্য বিনয়ী রিমাইন্ডার মেসেজ:**\n\n" +
                           "\"সম্মানিত গ্রাহক, আসসালামু আলাইকুম। BENE স্টোরে আপনার পূর্বের বকেয়া হিসাব রয়েছে। অনুগ্রহ করে হিসাবটি মিলিয়ে নিয়ে সুবিধাজনক সময়ে পরিশোধের অনুরোধ জানাচ্ছি। ধন্যবাদ!\"\n\n" +
                           "💡 অতিরিক্ত কঠোর না হয়ে বিনয়ের সাথে তাগাদা দিলে কাস্টমার রিলেশন নষ্ট হয় না।";
            return Ok(new { response = reply, reply = reply, success = true, timestamp = DateTime.UtcNow });
        }

        // Default to BunnyAiEngine (handles greetings, command tools)
        var result = await _aiEngine.ProcessCommandAsync(query, request.IsConfirmed, cancellationToken);
        return Ok(new
        {
            response = result.Message,
            reply = result.Message,
            success = result.Success,
            requiresConfirmation = result.RequiresConfirmation,
            confirmationPrompt = result.ConfirmationPrompt,
            targetScreen = result.TargetScreen,
            timestamp = DateTime.UtcNow
        });
    }

    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteCommand([FromBody] AiExecutionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Command))
            return BadRequest("কমান্ড প্রদান করুন।");

        var result = await _aiEngine.ProcessCommandAsync(req.Command, req.IsConfirmed ?? false);
        return Ok(result);
    }
}

public class AiChatRequest
{
    public string Prompt { get; set; } = string.Empty;
    public string? Context { get; set; }
    public bool IsConfirmed { get; set; }
}
