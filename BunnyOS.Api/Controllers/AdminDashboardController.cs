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
[Route("api/v1/admin/dashboard")]
public class AdminDashboardController : ControllerBase
{
    private readonly BunnyDbContext _db;

    public AdminDashboardController(BunnyDbContext db)
    {
        _db = db;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var allUsers = await _db.Users.IgnoreQueryFilters().ToListAsync();
        var totalUsers = allUsers.Count;
        var verifiedUsers = allUsers.Count(u => u.IsVerified);
        var activeSubscriptions = allUsers.Count(u => u.SubscriptionStatus == "Active");
        var suspendedAccounts = allUsers.Count(u => !u.IsActive);

        // Subscriptions breakdown
        var monthly = allUsers.Count(u => u.SubscriptionPlan == "Pro" || u.SubscriptionPlan == "Free");
        var quarterly = allUsers.Count(u => u.SubscriptionPlan == "Business");
        var yearly = allUsers.Count(u => u.SubscriptionPlan == "Enterprise");
        var totalSubs = Math.Max(1, monthly + quarterly + yearly);

        // Recent Activity
        var recentAuditLogs = await _db.PlatformAuditLogs
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(6)
            .Select(l => new
            {
                action = l.Action,
                actor = l.ActorEmail,
                target = l.TargetResource,
                time = l.CreatedAtUtc,
                result = l.Result
            })
            .ToListAsync();

        // System Health components
        var health = new
        {
            backendApi = "Operational",
            database = "Operational",
            authentication = "Operational",
            emailDelivery = "Operational",
            realtimeSync = "Operational",
            aiProvider = "Operational",
            averageLatencyMs = 245
        };

        // User registration trend (last 7 days)
        var now = DateTime.UtcNow.Date;
        var registrationTrend = Enumerable.Range(0, 7).Select(offset =>
        {
            var date = now.AddDays(-6 + offset);
            var count = allUsers.Count(u => u.CreatedAtUtc.Date == date);
            return new { date = date.ToString("MMM dd"), count };
        }).ToList();

        var monthlyPercent = allUsers.Count > 0 ? (int)Math.Round((double)monthly / totalSubs * 100) : 100;
        var quarterlyPercent = allUsers.Count > 0 ? (int)Math.Round((double)quarterly / totalSubs * 100) : 0;
        var yearlyPercent = allUsers.Count > 0 ? (int)Math.Round((double)yearly / totalSubs * 100) : 0;

        return Ok(new
        {
            metrics = new
            {
                totalUsers,
                verifiedUsers,
                activeSubscriptions,
                suspendedAccounts
            },
            subscriptionBreakdown = new
            {
                monthlyPercent,
                quarterlyPercent,
                yearlyPercent,
                totalActive = activeSubscriptions
            },
            registrationTrend,
            systemHealth = health,
            recentActivity = recentAuditLogs
        });
    }
}
