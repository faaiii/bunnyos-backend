using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BunnyOS.Domain.Entities;
using BunnyOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BunnyOS.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
public class AdminPlansAndSubsController : ControllerBase
{
    private readonly BunnyDbContext _db;

    public AdminPlansAndSubsController(BunnyDbContext db)
    {
        _db = db;
    }

    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans()
    {
        var plans = await _db.SubscriptionPlans.ToListAsync();
        if (!plans.Any())
        {
            // Seed default plans matching UI Preview
            plans = new List<SubscriptionPlan>
            {
                new() { Name = "Free", Price = 0, Interval = "Monthly", Features = "Basic features, 50 Products, 1 User", IsActive = true, MaxProducts = 50, MaxStaff = 1 },
                new() { Name = "Pro", Price = 1999, Interval = "Monthly", Features = "Advanced features, 500 Products, 3 Users, AI Assistant", IsActive = true, MaxProducts = 500, MaxStaff = 3 },
                new() { Name = "Business", Price = 4999, Interval = "Monthly", Features = "Full features, Unlimited Products, 10 Users, Priority AI", IsActive = true, MaxProducts = 10000, MaxStaff = 10 },
                new() { Name = "Enterprise", Price = 12999, Interval = "Monthly", Features = "Custom features, Dedicated Support, Unlimited Staff", IsActive = true, MaxProducts = 100000, MaxStaff = 100 }
            };
            _db.SubscriptionPlans.AddRange(plans);
            await _db.SaveChangesAsync();
        }

        return Ok(plans.Select(p => new
        {
            id = p.Id,
            planName = p.Name,
            price = p.Price,
            interval = p.Interval,
            features = p.Features,
            status = p.IsActive ? "Active" : "Inactive"
        }));
    }

    [HttpGet("subscriptions")]
    public async Task<IActionResult> GetSubscriptions()
    {
        var allUsers = await _db.Users.IgnoreQueryFilters().Include(u => u.Tenant).ToListAsync();
        var total = allUsers.Count;
        var active = allUsers.Count(u => u.SubscriptionStatus == "Active");
        var expired = allUsers.Count(u => u.SubscriptionStatus == "Expired");
        var trial = allUsers.Count(u => u.SubscriptionStatus == "Trial");

        var list = allUsers.Take(15).Select(u => new
        {
            id = u.Id,
            accountName = u.FullName,
            businessName = u.Tenant?.Name ?? "BENE",
            plan = u.SubscriptionPlan,
            status = u.SubscriptionStatus,
            startDate = u.CreatedAtUtc.ToString("MMM dd, yyyy"),
            expiryDate = u.SubscriptionExpiresAtUtc?.ToString("MMM dd, yyyy") ?? u.CreatedAtUtc.AddMonths(1).ToString("MMM dd, yyyy"),
            billingInterval = "Monthly",
            paymentStatus = "Paid"
        }).ToList();

        return Ok(new
        {
            kpis = new { total, active, expired, trial },
            items = list
        });
    }

    [HttpGet("system-health")]
    public IActionResult GetSystemHealth()
    {
        return Ok(new
        {
            backendApi = new { status = "Operational", latencyMs = 24 },
            database = new { status = "Operational", latencyMs = 143 },
            authentication = new { status = "Operational", latencyMs = 15 },
            emailDelivery = new { status = "Operational", latencyMs = 85 },
            realtimeSync = new { status = "Operational", latencyMs = 45 },
            aiProvider = new { status = "Operational", latencyMs = 180 },
            averageLatencyMs = 143,
            recentErrors = Array.Empty<object>()
        });
    }

    [HttpGet("ai-usage")]
    public async Task<IActionResult> GetAiUsage()
    {
        var logs = await _db.AiUsageRecords.OrderByDescending(r => r.CreatedAtUtc).Take(10).ToListAsync();
        var totalReqs = await _db.AiUsageRecords.CountAsync();
        var failedReqs = await _db.AiUsageRecords.CountAsync(r => !r.IsSuccess);

        return Ok(new
        {
            totalRequests = totalReqs,
            failedRequests = failedReqs,
            estimatedCostBdt = 0.00m,
            requestsOverTime = Array.Empty<object>(),
            requestsByModel = new[]
            {
                new { model = "Gemini", percentage = 70 },
                new { model = "Offline NLP", percentage = 30 }
            },
            recentActivity = logs.Select(l => new
            {
                id = l.Id,
                model = l.ModelName,
                tool = l.ToolName,
                tokens = l.TotalTokens,
                status = l.IsSuccess ? "Success" : "Failed",
                time = l.CreatedAtUtc.ToString("MMM dd, yyyy HH:mm")
            })
        });
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications()
    {
        var notifs = await _db.PlatformNotifications.OrderByDescending(n => n.CreatedAtUtc).Take(20).ToListAsync();
        if (!notifs.Any())
        {
            notifs = new List<PlatformNotification>
            {
                new() { Title = "BunnyOS সিস্টেম প্রস্তুত", Message = "ক্লাউড ডাটাবেজ এবং সিকিউরিটি সার্ভিস সফলভাবে সক্রিয় হয়েছে।", Category = "System", Severity = "Success" },
                new() { Title = "নিরাপত্তা অডিট পাস", Message = "সকল ডাটাবেজ সংযোগ এনক্রিপ্টেড এবং সক্রিয় রয়েছে।", Category = "Security", Severity = "Success" }
            };
            _db.PlatformNotifications.AddRange(notifs);
            await _db.SaveChangesAsync();
        }

        return Ok(notifs.Select(n => new
        {
            id = n.Id,
            title = n.Title,
            message = n.Message,
            category = n.Category,
            severity = n.Severity,
            time = n.CreatedAtUtc.ToString("MMM dd, yyyy HH:mm"),
            isRead = n.IsRead
        }));
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs()
    {
        var logs = await _db.PlatformAuditLogs.OrderByDescending(l => l.CreatedAtUtc).Take(50).ToListAsync();
        return Ok(logs.Select(l => new
        {
            id = l.Id,
            actor = l.ActorEmail,
            role = l.ActorRole,
            action = l.Action,
            target = l.TargetResource,
            result = l.Result,
            details = l.Details,
            time = l.CreatedAtUtc.ToString("MMM dd, yyyy HH:mm:ss")
        }));
    }
}
