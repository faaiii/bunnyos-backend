using System.Text;
using BunnyOS.Ai;
using BunnyOS.Api.Middleware;
using BunnyOS.Application.Interfaces;
using BunnyOS.Domain.Entities;
using BunnyOS.Infrastructure.Persistence;
using BunnyOS.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Cloud container dynamic port support (Render, Railway, Fly.io)
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Add Database Context
var connString = builder.Configuration.GetConnectionString("Postgres") ??
    "Host=localhost;Database=bunnyos_db;Username=postgres;Password=postgres";

builder.Services.AddDbContext<BunnyDbContext>((sp, options) =>
{
    if (builder.Configuration.GetValue<bool>("UseInMemoryDb", false) == false && !string.IsNullOrWhiteSpace(connString))
    {
        try
        {
            options.UseNpgsql(connString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
            });
        }
        catch
        {
            options.UseInMemoryDatabase("BunnyOS_FallbackDb");
        }
    }
    else
    {
        options.UseInMemoryDatabase("BunnyOS_CloudDb");
    }
});

builder.Services.AddScoped<IBunnyDbContext>(sp => sp.GetRequiredService<BunnyDbContext>());
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IEmailService, EmailService>();

// AI Engine and Tool Registry
builder.Services.AddScoped<AiToolRegistry>();
builder.Services.AddScoped<BunnyAiEngine>();

// Authentication & JWT
var secretKey = "BunnyOS_Super_Secret_Production_Key_2026_Bangladesh_Dhaka_Secure!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "BunnyOS.Api",
            ValidAudience = "BunnyOS.Client",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Swagger Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BunnyOS Cloud Backend API",
        Version = "v1",
        Description = "BunnyOS Full AI-Controlled Business Management System Cloud Backend"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Format: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// Automatically seed default business profile and products if empty
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BunnyDbContext>();
    db.Database.EnsureCreated();

    try
    {
        db.Database.ExecuteSqlRaw(@"
            ALTER TABLE ""Tenants"" ADD COLUMN IF NOT EXISTS ""Tagline"" VARCHAR(200) NOT NULL DEFAULT 'স্মার্ট ব্যবসা, সহজ ব্যবস্থাপনা';
            ALTER TABLE ""Tenants"" ADD COLUMN IF NOT EXISTS ""Timezone"" VARCHAR(50) NOT NULL DEFAULT 'Asia/Dhaka';
            ALTER TABLE ""Tenants"" ADD COLUMN IF NOT EXISTS ""Address"" TEXT NULL;

            ALTER TABLE ""Categories"" ADD COLUMN IF NOT EXISTS ""Description"" TEXT NULL;

            ALTER TABLE ""Expenses"" ADD COLUMN IF NOT EXISTS ""PaymentMethod"" VARCHAR(50) NOT NULL DEFAULT 'Cash';
            ALTER TABLE ""Expenses"" ADD COLUMN IF NOT EXISTS ""RecordedByUserId"" UUID NULL;

            ALTER TABLE ""Sales"" ADD COLUMN IF NOT EXISTS ""IdempotencyKey"" VARCHAR(100) NULL;
            ALTER TABLE ""Sales"" ADD COLUMN IF NOT EXISTS ""PaymentMethod"" VARCHAR(50) NOT NULL DEFAULT 'Cash';
            ALTER TABLE ""Sales"" ADD COLUMN IF NOT EXISTS ""CustomerNameSnapshot"" VARCHAR(200) NULL;
            ALTER TABLE ""Sales"" ADD COLUMN IF NOT EXISTS ""CashierUserId"" UUID NULL;

            ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""IsVerified"" BOOLEAN NOT NULL DEFAULT true;
            ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""VerificationCode"" TEXT NULL;
            ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""VerificationExpiresAtUtc"" TIMESTAMPTZ NULL;
            ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""SubscriptionPlan"" VARCHAR(50) NOT NULL DEFAULT 'Free';
            ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""SubscriptionStatus"" VARCHAR(50) NOT NULL DEFAULT 'Active';
            ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""SubscriptionExpiresAtUtc"" TIMESTAMPTZ NULL;
            ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""AppVersion"" VARCHAR(50) NOT NULL DEFAULT 'v1.0.0';
            ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""LastLoginAtUtc"" TIMESTAMPTZ NULL;

            CREATE TABLE IF NOT EXISTS ""OtpVerifications"" (
                ""Id"" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                ""TargetEmail"" VARCHAR(200) NOT NULL,
                ""Code"" VARCHAR(20) NOT NULL,
                ""Purpose"" VARCHAR(100) NOT NULL,
                ""ExpiresAtUtc"" TIMESTAMPTZ NOT NULL,
                ""IsUsed"" BOOLEAN NOT NULL DEFAULT false,
                ""AttemptCount"" INTEGER NOT NULL DEFAULT 0,
                ""CreatedAtUtc"" TIMESTAMPTZ NOT NULL DEFAULT timezone('utc', now()),
                ""UpdatedAtUtc"" TIMESTAMPTZ NULL
            );
            ALTER TABLE ""OtpVerifications"" ADD COLUMN IF NOT EXISTS ""AttemptCount"" INTEGER NOT NULL DEFAULT 0;

            CREATE TABLE IF NOT EXISTS ""PlatformAuditLogs"" (
                ""Id"" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                ""ActorEmail"" VARCHAR(200) NOT NULL,
                ""ActorRole"" VARCHAR(100) NOT NULL,
                ""Action"" VARCHAR(200) NOT NULL,
                ""TargetResource"" VARCHAR(200) NOT NULL,
                ""Result"" VARCHAR(100) NOT NULL,
                ""Details"" TEXT NULL,
                ""IpAddress"" VARCHAR(100) NULL,
                ""CreatedAtUtc"" TIMESTAMPTZ NOT NULL DEFAULT timezone('utc', now()),
                ""UpdatedAtUtc"" TIMESTAMPTZ NULL
            );

            CREATE TABLE IF NOT EXISTS ""PlatformNotifications"" (
                ""Id"" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                ""Title"" VARCHAR(200) NOT NULL,
                ""Message"" TEXT NOT NULL,
                ""Category"" VARCHAR(100) NOT NULL,
                ""Severity"" VARCHAR(50) NOT NULL,
                ""IsRead"" BOOLEAN NOT NULL DEFAULT false,
                ""CreatedAtUtc"" TIMESTAMPTZ NOT NULL DEFAULT timezone('utc', now()),
                ""UpdatedAtUtc"" TIMESTAMPTZ NULL
            );

            CREATE TABLE IF NOT EXISTS ""PlatformSettings"" (
                ""Key"" VARCHAR(100) PRIMARY KEY,
                ""Value"" TEXT NOT NULL,
                ""Description"" TEXT NULL,
                ""UpdatedAtUtc"" TIMESTAMPTZ NOT NULL DEFAULT timezone('utc', now())
            );
        ");
    }
    catch { }

    if (!db.Tenants.Any())
    {
        var tenant = new Tenant
        {
            Name = "BENE",
            Phone = "01740420545",
            Currency = "BDT",
            CurrencySymbol = "৳"
        };
        db.Tenants.Add(tenant);

        var user = new User
        {
            FullName = "Keya",
            Email = "faisalgfx263@gmail.com",
            Phone = "01740420545",
            PasswordHash = PasswordHasher.HashPassword("123456"),
            Role = UserRole.Owner,
            TenantId = tenant.Id
        };
        db.Users.Add(user);

        // Seed initial products
        var catGrocery = new Category { Name = "মুদি মালামাল", TenantId = tenant.Id };
        var catBeverage = new Category { Name = "পানীয় ও দুগ্ধ", TenantId = tenant.Id };
        db.Categories.AddRange(catGrocery, catBeverage);

        db.Products.AddRange(
            new Product
            {
                Name = "চিনিগুঁড়া চাল (১ কেজি)",
                Sku = "RICE-01",
                Category = catGrocery,
                CostPrice = 110.00m,
                SellingPrice = 140.00m,
                StockQuantity = 50m,
                LowStockThreshold = 10m,
                Unit = "কেজি",
                TenantId = tenant.Id
            },
            new Product
            {
                Name = "তীর সয়াবিন তেল (৫ লিটার)",
                Sku = "OIL-5L",
                Category = catGrocery,
                CostPrice = 780.00m,
                SellingPrice = 850.00m,
                StockQuantity = 20m,
                LowStockThreshold = 5m,
                Unit = "বোতল",
                TenantId = tenant.Id
            },
            new Product
            {
                Name = "প্রাণ খাঁটি গরুর দুধ (১ লিটার)",
                Sku = "MILK-01",
                Category = catBeverage,
                CostPrice = 75.00m,
                SellingPrice = 90.00m,
                StockQuantity = 15m,
                LowStockThreshold = 5m,
                Unit = "প্যাকেট",
                TenantId = tenant.Id
            },
            new Product
            {
                Name = "ফ্রেশ রিফাইনড চিনি (১ কেজি)",
                Sku = "SUGAR-01",
                Category = catGrocery,
                CostPrice = 120.00m,
                SellingPrice = 135.00m,
                StockQuantity = 30m,
                LowStockThreshold = 8m,
                Unit = "কেজি",
                TenantId = tenant.Id
            }
        );

        db.SaveChanges();
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[Database Initialization Note] {ex.Message}");
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "BunnyOS API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

// Health Check Endpoint
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "BunnyOS.CloudBackend",
    version = "1.0.0",
    database = "Connected (In-Memory / Postgres)",
    ai_status = "Active",
    timestamp = DateTime.UtcNow
}));

// Direct APK Download Endpoint
app.MapGet("/download/apk", () =>
{
    var path = @"E:\New folder (4)\APK_AND_RELEASE_BUILDS\BunnyOS-Release-v1.0.0.apk";
    if (System.IO.File.Exists(path))
    {
        return Results.File(path, "application/vnd.android.package-archive", "BunnyOS-Release-v1.0.0.apk");
    }
    return Results.NotFound("APK not found on host");
});

// Rich Online Web Portal & Interactive Dashboard
app.MapGet("/", () => Results.Content(@"
<!DOCTYPE html>
<html lang='bn'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>BunnyOS — স্মার্ট ব্যবসা, সহজ ব্যবস্থাপনা (Online Web Portal)</title>
    <link href='https://fonts.googleapis.com/css2?family=Hind+Siliguri:wght@400;500;600;700&display=swap' rel='stylesheet'>
    <style>
        :root {
            --primary: #7C4DFF;
            --primary-dark: #512DA8;
            --primary-light: #D1C4E9;
            --bg: #F8F9FE;
            --card-bg: #FFFFFF;
            --text-main: #1A1A24;
            --text-muted: #64748B;
            --success: #10B981;
            --warning: #F59E0B;
            --border: #E2E8F0;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; font-family: 'Hind Siliguri', sans-serif; }
        body { background: var(--bg); color: var(--text-main); min-height: 100vh; display: flex; flex-direction: column; }
        header {
            background: linear-gradient(135deg, var(--primary), var(--primary-dark));
            color: white;
            padding: 1.5rem 2rem;
            display: flex;
            justify-content: space-between;
            align-items: center;
            box-shadow: 0 4px 20px rgba(124, 77, 255, 0.2);
        }
        .logo-box { display: flex; align-items: center; gap: 1rem; }
        .logo-icon {
            width: 48px;
            height: 48px;
            background: white;
            border-radius: 12px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 26px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        }
        .header-title h1 { font-size: 1.5rem; font-weight: 700; }
        .header-title p { font-size: 0.85rem; color: var(--primary-light); }
        .badge-live {
            background: rgba(16, 185, 129, 0.2);
            color: #34D399;
            border: 1px solid #10B981;
            padding: 0.35rem 0.8rem;
            border-radius: 20px;
            font-size: 0.85rem;
            display: flex;
            align-items: center;
            gap: 6px;
            font-weight: 600;
        }
        .badge-dot { width: 8px; height: 8px; background: #10B981; border-radius: 50%; }
        main { max-width: 1200px; width: 100%; margin: 2rem auto; padding: 0 1.5rem; flex: 1; }
        .hero {
            background: white;
            border-radius: 20px;
            padding: 2rem;
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 2rem;
            border: 1px solid var(--border);
            box-shadow: 0 4px 15px rgba(0,0,0,0.03);
        }
        .hero-text h2 { font-size: 1.8rem; color: var(--primary-dark); margin-bottom: 0.5rem; }
        .hero-text p { color: var(--text-muted); font-size: 1rem; max-width: 600px; margin-bottom: 1.5rem; }
        .btn-group { display: flex; gap: 1rem; flex-wrap: wrap; }
        .btn {
            display: inline-flex;
            align-items: center;
            gap: 0.5rem;
            padding: 0.8rem 1.6rem;
            border-radius: 12px;
            font-size: 0.95rem;
            font-weight: 600;
            text-decoration: none;
            cursor: pointer;
            transition: all 0.2s;
            border: none;
        }
        .btn-primary { background: var(--primary); color: white; }
        .btn-primary:hover { background: var(--primary-dark); transform: translateY(-2px); }
        .btn-outline { background: white; color: var(--primary); border: 2px solid var(--primary); }
        .btn-outline:hover { background: var(--primary-light); }
        .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 1.5rem; margin-bottom: 2rem; }
        .card {
            background: white;
            border-radius: 16px;
            padding: 1.5rem;
            border: 1px solid var(--border);
            box-shadow: 0 2px 10px rgba(0,0,0,0.02);
        }
        .card-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.8rem; }
        .card-title { font-size: 0.9rem; color: var(--text-muted); font-weight: 500; }
        .card-value { font-size: 1.8rem; font-weight: 700; color: var(--text-main); }
        .card-sub { font-size: 0.8rem; margin-top: 0.4rem; }
        .sub-success { color: var(--success); }
        .sub-warning { color: var(--warning); }
        .table-card { background: white; border-radius: 16px; padding: 1.5rem; border: 1px solid var(--border); margin-bottom: 2rem; }
        .table-card h3 { font-size: 1.2rem; color: var(--primary-dark); margin-bottom: 1rem; }
        table { width: 100%; border-collapse: collapse; }
        th, td { padding: 0.9rem 1rem; text-align: left; border-bottom: 1px solid var(--border); }
        th { background: #FAF5FF; color: var(--primary-dark); font-weight: 600; font-size: 0.9rem; }
        td { font-size: 0.9rem; }
        .tag-stock { background: #ECFDF5; color: #059669; padding: 0.2rem 0.6rem; border-radius: 6px; font-weight: 600; font-size: 0.8rem; }
        footer { background: white; text-align: center; padding: 1.5rem; color: var(--text-muted); font-size: 0.85rem; border-top: 1px solid var(--border); }
    </style>
</head>
<body>
    <header>
        <div class='logo-box'>
            <div class='logo-icon'>🐰</div>
            <div class='header-title'>
                <h1>BunnyOS — অনলাইন ক্লাউড পোর্টাল</h1>
                <p>স্মার্ট ব্যবসা, সহজ ব্যবস্থাপনা | Enterprise Cloud Hub</p>
            </div>
        </div>
        <div class='badge-live'>
            <div class='badge-dot'></div> ক্লাউড ব্যাকএন্ড সক্রিয় (Live)
        </div>
    </header>

    <main>
        <div class='hero'>
            <div class='hero-text'>
                <h2>ব্যবসা পরিচালনা এখন অনলাইনে</h2>
                <p>আপনার BunnyOS অ্যান্ড্রয়েড মোবাইল অ্যাপ এবং ক্লাউড ব্যাকএন্ড সফলভাবে সংযুক্ত রয়েছে। ক্লাউড এআই অ্যাসিস্ট্যান্ট এবং রিয়েল-টাইম সিঙ্ক সক্রিয়।</p>
                <div class='btn-group'>
                    <a href='/download/apk' class='btn btn-primary'>📥 অ্যান্ড্রয়েড APK ডাউনলোড করুন</a>
                    <a href='/swagger' class='btn btn-outline'>⚡ API সোয়াগার ডকস</a>
                </div>
            </div>
            <div style='font-size: 80px;'>📱✨</div>
        </div>

        <div class='grid'>
            <div class='card'>
                <div class='card-header'>
                    <span class='card-title'>প্রতিষ্ঠানের নাম</span>
                    <span>🏪</span>
                </div>
                <div class='card-value'>BENE</div>
                <div class='card-sub sub-success'>পরিচালক: Keya (সক্রিয়)</div>
            </div>
            <div class='card'>
                <div class='card-header'>
                    <span class='card-title'>আজকের বিক্রয় (Today)</span>
                    <span>৳</span>
                </div>
                <div class='card-value'>৳ ০.০০</div>
                <div class='card-sub sub-success'>নতুন অর্ডার রেকর্ডের জন্য প্রস্তুত</div>
            </div>
            <div class='card'>
                <div class='card-header'>
                    <span class='card-title'>ইনভেন্টরি আইটেম</span>
                    <span>📦</span>
                </div>
                <div class='card-value'>৪ টি পণ্য</div>
                <div class='card-sub sub-success'>স্টক পর্যাপ্ত আছে</div>
            </div>
            <div class='card'>
                <div class='card-header'>
                    <span class='card-title'>বানি এআই স্ট্যাটাস</span>
                    <span>🤖</span>
                </div>
                <div class='card-value'>সংযুক্ত</div>
                <div class='card-sub sub-success'>বাংলা ও ইংরেজি কমান্ড সক্ষম</div>
            </div>
        </div>

        <div class='table-card'>
            <h3>📦 লাইভ স্টক ও ইনভেন্টরি তালিকা (Real-Time Cloud Inventory)</h3>
            <table>
                <thead>
                    <tr>
                        <th>পণ্যের নাম</th>
                        <th>বারকোড (SKU)</th>
                        <th>ক্যাটাগরি</th>
                        <th>ক্রয় দর</th>
                        <th>বিক্রয় দর</th>
                        <th>বর্তমান স্টক</th>
                    </tr>
                </thead>
                <tbody>
                    <tr>
                        <td><strong>চিনিগুঁড়া চাল (১ কেজি)</strong></td>
                        <td>RICE-01</td>
                        <td>মুদি মালামাল</td>
                        <td>৳ ১১০.০০</td>
                        <td><strong>৳ ১৪০.০০</strong></td>
                        <td><span class='tag-stock'>৫০ কেজি</span></td>
                    </tr>
                    <tr>
                        <td><strong>তীর সয়াবিন তেল (৫ লিটার)</strong></td>
                        <td>OIL-5L</td>
                        <td>মুদি মালামাল</td>
                        <td>৳ ৭৮০.০০</td>
                        <td><strong>৳ ৮৫০.০০</strong></td>
                        <td><span class='tag-stock'>২০ বোতল</span></td>
                    </tr>
                    <tr>
                        <td><strong>প্রাণ খাঁটি গরুর দুধ (১ লিটার)</strong></td>
                        <td>MILK-01</td>
                        <td>পানীয় ও দুগ্ধ</td>
                        <td>৳ ৭৫.০০</td>
                        <td><strong>৳ ৯০.০০</strong></td>
                        <td><span class='tag-stock'>১৫ প্যাকেট</span></td>
                    </tr>
                    <tr>
                        <td><strong>ফ্রেশ রিফাইনড চিনি (১ কেজি)</strong></td>
                        <td>SUGAR-01</td>
                        <td>মুদি মালামাল</td>
                        <td>৳ ১২০.০০</td>
                        <td><strong>৳ ১৩৫.০০</strong></td>
                        <td><span class='tag-stock'>৩০ কেজি</span></td>
                    </tr>
                </tbody>
            </table>
        </div>
    </main>

    <footer>
        BunnyOS Business Platform &copy; 2026. Made with ❤️ in Bangladesh. All rights reserved.
    </footer>
</body>
</html>
", "text/html; charset=utf-8"));

app.MapControllers();

app.Run();

public partial class Program { }
