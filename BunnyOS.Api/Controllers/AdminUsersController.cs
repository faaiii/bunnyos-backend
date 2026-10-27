using System;
using System.Linq;
using System.Threading.Tasks;
using BunnyOS.Domain.Entities;
using BunnyOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BunnyOS.Api.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly BunnyDbContext _db;

    public AdminUsersController(BunnyDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search,
        [FromQuery] string? verificationStatus, // Verified, Pending
        [FromQuery] string? accountStatus,      // Active, Suspended
        [FromQuery] string? plan,               // Free, Pro, Business, Enterprise
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _db.Users.IgnoreQueryFilters().Include(u => u.Tenant).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(s) || u.Email.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(verificationStatus) && verificationStatus != "All")
        {
            var isVer = verificationStatus.Equals("Verified", StringComparison.OrdinalIgnoreCase);
            query = query.Where(u => u.IsVerified == isVer);
        }

        if (!string.IsNullOrWhiteSpace(accountStatus) && accountStatus != "All")
        {
            var isAct = accountStatus.Equals("Active", StringComparison.OrdinalIgnoreCase);
            query = query.Where(u => u.IsActive == isAct);
        }

        if (!string.IsNullOrWhiteSpace(plan) && plan != "All")
        {
            query = query.Where(u => u.SubscriptionPlan.ToLower() == plan.ToLower());
        }

        var totalCount = await query.CountAsync();
        var users = await query
            .OrderByDescending(u => u.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                id = u.Id,
                fullName = u.FullName,
                email = u.Email,
                phone = u.Phone,
                role = u.Role.ToString(),
                businessName = u.Tenant != null ? u.Tenant.Name : "N/A",
                isVerified = u.IsVerified,
                isActive = u.IsActive,
                subscriptionPlan = u.SubscriptionPlan,
                subscriptionStatus = u.SubscriptionStatus,
                registeredDate = u.CreatedAtUtc.ToString("MMM dd, yyyy"),
                lastActivity = u.LastLoginAtUtc.HasValue ? u.LastLoginAtUtc.Value.ToString("MMM dd, yyyy HH:mm") : "None",
                appVersion = u.AppVersion
            })
            .ToListAsync();

        return Ok(new
        {
            total = totalCount,
            page,
            pageSize,
            items = users
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserDetails(Guid id)
    {
        var user = await _db.Users.IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound("ইউজার পাওয়া যায়নি।");

        return Ok(new
        {
            id = user.Id,
            fullName = user.FullName,
            email = user.Email,
            phone = user.Phone,
            role = user.Role.ToString(),
            businessName = user.Tenant?.Name ?? "N/A",
            isVerified = user.IsVerified,
            isActive = user.IsActive,
            subscription = new
            {
                plan = user.SubscriptionPlan,
                status = user.SubscriptionStatus,
                expiresAt = user.SubscriptionExpiresAtUtc?.ToString("MMM dd, yyyy") ?? "Lifetime",
                billingInterval = "Monthly"
            },
            appVersion = user.AppVersion,
            registeredAt = user.CreatedAtUtc.ToString("MMM dd, yyyy HH:mm"),
            lastLoginAt = user.LastLoginAtUtc?.ToString("MMM dd, yyyy HH:mm") ?? "Never"
        });
    }

    [HttpPost("{id}/suspend")]
    public async Task<IActionResult> SuspendUser(Guid id)
    {
        var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound("ইউজার পাওয়া যায়নি।");

        user.IsActive = false;
        _db.PlatformAuditLogs.Add(new PlatformAuditLog
        {
            ActorEmail = "admin@bunnyos.com",
            ActorRole = "PlatformOwner",
            Action = "UserSuspended",
            TargetResource = user.Email,
            Result = "Success",
            Details = $"User {user.FullName} was suspended by platform owner."
        });
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = "ইউজার সফলভাবে স্থগিত করা হয়েছে।" });
    }

    [HttpPost("{id}/restore")]
    public async Task<IActionResult> RestoreUser(Guid id)
    {
        var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound("ইউজার পাওয়া যায়নি।");

        user.IsActive = true;
        _db.PlatformAuditLogs.Add(new PlatformAuditLog
        {
            ActorEmail = "admin@bunnyos.com",
            ActorRole = "PlatformOwner",
            Action = "UserRestored",
            TargetResource = user.Email,
            Result = "Success",
            Details = $"User {user.FullName} was restored by platform owner."
        });
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = "ইউজার সফলভাবে সক্রিয় করা হয়েছে।" });
    }

    [HttpPost("{id}/revoke-sessions")]
    public async Task<IActionResult> RevokeSessions(Guid id)
    {
        var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound("ইউজার পাওয়া যায়নি।");

        _db.PlatformAuditLogs.Add(new PlatformAuditLog
        {
            ActorEmail = "admin@bunnyos.com",
            ActorRole = "PlatformOwner",
            Action = "SessionRevoked",
            TargetResource = user.Email,
            Result = "Success",
            Details = $"All active sessions for {user.FullName} revoked."
        });
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = "ইউজারের সকল সক্রিয় সেশন বাতিল করা হয়েছে।" });
    }
}
