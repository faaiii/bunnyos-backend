using System;
using System.Linq;
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
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly BunnyDbContext _db;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IEmailService _emailService;

    public AuthController(BunnyDbContext db, IJwtTokenGenerator jwt, IEmailService emailService)
    {
        _db = db;
        _jwt = jwt;
        _emailService = emailService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("ইমেইল এবং পাসওয়ার্ড আবশ্যক।");

        var cleanEmail = req.Email.Trim().ToLowerInvariant();
        var existingUser = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email.ToLower() == cleanEmail);
        if (existingUser != null)
            return Conflict("এই ইমেইল দিয়ে ইতোমধ্যে একটি অ্যাকাউন্ট রয়েছে।");

        var tenant = new Tenant
        {
            Name = string.IsNullOrWhiteSpace(req.BusinessName) ? "আমার ব্যবসা" : req.BusinessName,
            Phone = req.Phone ?? string.Empty,
            Currency = "BDT",
            CurrencySymbol = "৳"
        };
        _db.Tenants.Add(tenant);

        var user = new User
        {
            FullName = req.FullName,
            Email = cleanEmail,
            Phone = req.Phone ?? string.Empty,
            PasswordHash = PasswordHasher.HashPassword(req.Password),
            Role = UserRole.Owner,
            TenantId = tenant.Id,
            IsVerified = false
        };
        _db.Users.Add(user);

        // Generate 6-digit OTP code
        var random = new Random();
        var otpCode = random.Next(100000, 999999).ToString();
        var otp = new OtpVerification
        {
            TargetEmail = user.Email,
            Code = otpCode,
            Purpose = "EmailVerification",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            IsUsed = false
        };
        _db.OtpVerifications.Add(otp);

        _db.PlatformAuditLogs.Add(new PlatformAuditLog
        {
            ActorEmail = user.Email,
            ActorRole = user.Role.ToString(),
            Action = "CustomerRegistration",
            TargetResource = tenant.Name,
            Result = "PendingOtp",
            Details = $"New business registered: {tenant.Name} by {user.FullName} ({user.Email})",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        await _db.SaveChangesAsync();

        // Send OTP verification email in background without blocking API response
        var targetEmail = user.Email;
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailService.SendOtpEmailAsync(targetEmail, otpCode, "BunnyOS অ্যাকাউন্ট ভেরিফিকেশন");
            }
            catch { }
        });

        return Ok(new
        {
            requiresVerification = true,
            email = user.Email,
            message = "৬-ডিজিটের ভেরিফিকেশন কোড আপনার ইমেইলে পাঠানো হয়েছে।",
            devCode = otpCode
        });
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Code))
            return BadRequest("ইমেইল এবং কোড আবশ্যক।");

        var cleanEmail = req.Email.Trim().ToLowerInvariant();
        var otp = await _db.OtpVerifications
            .Where(o => o.TargetEmail.ToLower() == cleanEmail && o.Code == req.Code && !o.IsUsed)
            .OrderByDescending(o => o.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (otp == null || otp.ExpiresAtUtc < DateTime.UtcNow)
            return Unauthorized("ভেরিফিকেশন কোডটি ভুল অথবা মেয়াদ শেষ হয়ে গেছে।");

        otp.IsUsed = true;

        var user = await _db.Users.IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == cleanEmail);

        if (user == null || user.Tenant == null)
            return Unauthorized("ইউজার প্রোফাইল পাওয়া যায়নি।");

        user.IsVerified = true;
        user.LastLoginAtUtc = DateTime.UtcNow;

        _db.PlatformAuditLogs.Add(new PlatformAuditLog
        {
            ActorEmail = user.Email,
            ActorRole = user.Role.ToString(),
            Action = "AccountVerified",
            TargetResource = user.Tenant.Name,
            Result = "Success",
            Details = $"User {user.FullName} ({user.Email}) successfully verified OTP and activated business profile.",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        await _db.SaveChangesAsync();

        var token = _jwt.GenerateToken(user, user.Tenant);
        return Ok(new
        {
            token,
            tenantId = user.Tenant.Id,
            userId = user.Id,
            fullName = user.FullName,
            role = user.Role.ToString(),
            businessName = user.Tenant.Name,
            currencySymbol = user.Tenant.CurrencySymbol,
            message = "অ্যাকাউন্ট ভেরিফিকেশন সফল হয়েছে।"
        });
    }

    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest("ইমেইল আবশ্যক।");

        var cleanEmail = req.Email.Trim().ToLowerInvariant();
        var random = new Random();
        var otpCode = random.Next(100000, 999999).ToString();
        var otp = new OtpVerification
        {
            TargetEmail = cleanEmail,
            Code = otpCode,
            Purpose = "EmailVerification",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            IsUsed = false
        };
        _db.OtpVerifications.Add(otp);
        await _db.SaveChangesAsync();

        await _emailService.SendOtpEmailAsync(cleanEmail, otpCode, "BunnyOS অ্যাকাউন্ট ভেরিফিকেশন (পুনরায় প্রেরিত)");

        return Ok(new
        {
            success = true,
            message = "নতুন ভেরিফিকেশন কোড পাঠানো হয়েছে।",
            devCode = otpCode
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var cleanEmail = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.IgnoreQueryFilters().Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Email.ToLower() == cleanEmail);
        if (user == null || !PasswordHasher.VerifyPassword(req.Password, user.PasswordHash))
            return Unauthorized("ভুল ইমেইল অথবা পাসওয়ার্ড।");

        if (user.Tenant == null)
            return Unauthorized("ব্যবসায়িক প্রোফাইল পাওয়া যায়নি।");

        // Generate 6-digit OTP code for per-user Login Verification
        var random = new Random();
        var otpCode = random.Next(100000, 999999).ToString();
        var otp = new OtpVerification
        {
            TargetEmail = user.Email,
            Code = otpCode,
            Purpose = "LoginVerification",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            IsUsed = false
        };
        _db.OtpVerifications.Add(otp);

        _db.PlatformAuditLogs.Add(new PlatformAuditLog
        {
            ActorEmail = user.Email,
            ActorRole = user.Role.ToString(),
            Action = "CustomerLoginAttempt",
            TargetResource = user.Tenant.Name,
            Result = "PendingOtp",
            Details = $"User {user.FullName} ({user.Email}) initiated login into store '{user.Tenant.Name}'. OTP sent to {user.Email}.",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        await _db.SaveChangesAsync();

        // Send OTP verification email in background without blocking API response
        var targetEmail = user.Email;
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailService.SendOtpEmailAsync(targetEmail, otpCode, "BunnyOS লগইন ভেরিফিকেশন");
            }
            catch { }
        });

        return Ok(new
        {
            requires2Fa = true,
            email = user.Email,
            message = "৬-ডিজিটের ভেরিফিকেশন কোড আপনার ইমেইলে পাঠানো হয়েছে।",
            devCode = otpCode
        });
    }
}

public record ResendOtpRequest(string Email);

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly BunnyDbContext _db;
    public ProductsController(BunnyDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] bool lowStockOnly = false)
    {
        var query = _db.Products.Include(p => p.Category).Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => p.Name.Contains(search) || p.Sku.Contains(search));
        }

        if (lowStockOnly)
        {
            query = query.Where(p => p.StockQuantity <= p.LowStockThreshold);
        }

        var list = await query.OrderBy(p => p.Name).Select(p => new ProductDto(
            p.Id,
            p.Name,
            p.Sku,
            p.CategoryId,
            p.Category != null ? p.Category.Name : null,
            p.CostPrice,
            p.SellingPrice,
            p.StockQuantity,
            p.LowStockThreshold,
            p.Unit,
            p.StockQuantity <= p.LowStockThreshold
        )).ToListAsync();

        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("পণ্যের নাম আবশ্যক।");

        var sku = string.IsNullOrWhiteSpace(req.Sku) ? "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant() : req.Sku;

        var product = new Product
        {
            Name = req.Name,
            Sku = sku,
            CategoryId = req.CategoryId,
            CostPrice = req.CostPrice,
            SellingPrice = req.SellingPrice,
            StockQuantity = req.StockQuantity,
            LowStockThreshold = req.LowStockThreshold,
            Unit = string.IsNullOrWhiteSpace(req.Unit) ? "Pcs" : req.Unit,
            IsActive = true
        };

        _db.Products.Add(product);

        if (req.StockQuantity > 0)
        {
            _db.StockMovements.Add(new StockMovement
            {
                Product = product,
                QuantityChange = req.StockQuantity,
                PreviousQuantity = 0,
                NewQuantity = req.StockQuantity,
                MovementType = StockMovementType.InitialStock,
                Reason = "প্রাথমিক স্টক এন্ট্রি"
            });
        }

        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { id = product.Id }, product);
    }

    [HttpPost("adjust-stock")]
    public async Task<IActionResult> AdjustStock([FromBody] StockAdjustmentRequest req)
    {
        var product = await _db.Products.FindAsync(req.ProductId);
        if (product == null) return NotFound("পণ্য পাওয়া যায়নি।");

        var oldQty = product.StockQuantity;
        var newQty = oldQty + req.QuantityChange;

        product.StockQuantity = newQty;
        _db.StockMovements.Add(new StockMovement
        {
            ProductId = product.Id,
            QuantityChange = req.QuantityChange,
            PreviousQuantity = oldQty,
            NewQuantity = newQty,
            MovementType = StockMovementType.ManualAdjustment,
            Reason = req.Reason ?? "ম্যানুয়াল সমন্বয়"
        });

        await _db.SaveChangesAsync();
        return Ok(new { product.Id, product.StockQuantity });
    }
}
