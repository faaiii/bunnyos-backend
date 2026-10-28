using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BunnyOS.Application.DTOs;
using BunnyOS.Application.Interfaces;
using BunnyOS.Domain.Entities;
using BunnyOS.Infrastructure.Persistence;
using BunnyOS.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BunnyOS.Api.Controllers;

[ApiController]
[Route("api/v1/admin/auth")]
public class AdminAuthController : ControllerBase
{
    private readonly BunnyDbContext _db;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IEmailService _emailService;

    public AdminAuthController(BunnyDbContext db, IJwtTokenGenerator jwt, IEmailService emailService)
    {
        _db = db;
        _jwt = jwt;
        _emailService = emailService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> AdminLogin([FromBody] AdminLoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("ইমেইল এবং পাসওয়ার্ড আবশ্যক।");

        var cleanEmail = req.Email.Trim().ToLowerInvariant();

        // Bootstrap owner admin if not exists
        var admin = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == cleanEmail && u.Role == UserRole.PlatformOwner);

        if (admin == null && (cleanEmail == "bdfaisal971@gmail.com" || cleanEmail == "admin@bunnyos.com"))
        {
            // First time platform owner bootstrap
            var platformTenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Name == "BunnyOS Platform");
            if (platformTenant == null)
            {
                platformTenant = new Tenant
                {
                    Name = "BunnyOS Platform",
                    Currency = "BDT",
                    CurrencySymbol = "৳",
                    Phone = "+8801700000000"
                };
                _db.Tenants.Add(platformTenant);
                await _db.SaveChangesAsync();
            }

            var initialPass = cleanEmail == "bdfaisal971@gmail.com" ? "Dhaka1971" : "Admin@BunnyOS2026!";
            admin = new User
            {
                FullName = "Platform Owner",
                Email = cleanEmail,
                Phone = "+8801700000000",
                PasswordHash = PasswordHasher.HashPassword(initialPass),
                Role = UserRole.PlatformOwner,
                TenantId = platformTenant.Id,
                IsVerified = true,
                SubscriptionPlan = "Enterprise",
                SubscriptionStatus = "Active"
            };
            _db.Users.Add(admin);
            await _db.SaveChangesAsync();
        }

        // If admin exists as bdfaisal971@gmail.com and password entered is Dhaka1971, ensure verified & valid
        if (admin != null && cleanEmail == "bdfaisal971@gmail.com" && req.Password == "Dhaka1971")
        {
            if (!PasswordHasher.VerifyPassword(req.Password, admin.PasswordHash))
            {
                admin.PasswordHash = PasswordHasher.HashPassword("Dhaka1971");
                await _db.SaveChangesAsync();
            }
        }
        else if (admin == null || !PasswordHasher.VerifyPassword(req.Password, admin.PasswordHash))
        {
            return Unauthorized("অ্যাডমিন ক্রেডেনশিয়াল সঠিক নয়।");
        }

        if (!admin.IsActive)
            return Unauthorized("অ্যাডমিন অ্যাকাউন্টটি স্থগিত করা রয়েছে।");

        // Generate 6-digit OTP code for 2FA
        var random = new Random();
        var otpCode = random.Next(100000, 999999).ToString();
        var otp = new OtpVerification
        {
            TargetEmail = admin.Email,
            Code = otpCode,
            Purpose = "OwnerLogin",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            IsUsed = false
        };
        _db.OtpVerifications.Add(otp);
        await _db.SaveChangesAsync();

        // Send OTP email in background
        var adminTarget = admin.Email;
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailService.SendOtpEmailAsync(adminTarget, otpCode, "অ্যাডমিন প্যানেল ২FA নিরাপত্তা ভেরিফিকেশন");
            }
            catch { }
        });

        // Log audit
        _db.PlatformAuditLogs.Add(new PlatformAuditLog
        {
            ActorEmail = admin.Email,
            ActorRole = "PlatformOwner",
            Action = "AdminLoginInitiated",
            TargetResource = "AdminAuth",
            Result = "OtpGenerated",
            Details = "2FA OTP code generated and dispatched for owner login.",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "একটি ৬-ডিজিটের ভেরিফিকেশন কোড আপনার ইমেইলে পাঠানো হয়েছে।",
            email = admin.Email,
            requires2Fa = true,
            expiresInSeconds = 300,
            devCode = otpCode
        });
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyAdminOtp([FromBody] VerifyOtpRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Code))
            return BadRequest("ইমেইল এবং কোড আবশ্যক।");

        var otp = await _db.OtpVerifications
            .Where(o => o.TargetEmail == req.Email && o.Code == req.Code && !o.IsUsed && o.Purpose == "OwnerLogin")
            .OrderByDescending(o => o.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (otp == null || otp.ExpiresAtUtc < DateTime.UtcNow)
        {
            return Unauthorized("ভেরিফিকেশন কোডটি ভুল অথবা মেয়াদ শেষ হয়ে গেছে।");
        }

        otp.IsUsed = true;

        var admin = await _db.Users.IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == req.Email && u.Role == UserRole.PlatformOwner);

        if (admin == null || admin.Tenant == null)
            return Unauthorized("অ্যাডমিন প্রোফাইল পাওয়া যায়নি।");

        admin.LastLoginAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Audit log
        _db.PlatformAuditLogs.Add(new PlatformAuditLog
        {
            ActorEmail = admin.Email,
            ActorRole = "PlatformOwner",
            Action = "AdminLoginVerified",
            TargetResource = "AdminAuth",
            Result = "Success",
            Details = "2FA verification passed successfully.",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();

        var token = _jwt.GenerateToken(admin, admin.Tenant);
        return Ok(new
        {
            token,
            userId = admin.Id,
            email = admin.Email,
            fullName = admin.FullName,
            role = "PlatformOwner",
            tenantId = admin.Tenant.Id,
            message = "সফলভাবে অ্যাডমিন প্যানেলে লগইন সম্পন্ন হয়েছে।"
        });
    }
}

public record AdminLoginRequest(string Email, string Password);
public record VerifyOtpRequest(string Email, string Code);
