using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BunnyOS.Application.Interfaces;
using BunnyOS.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace BunnyOS.Infrastructure.Services;

public class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; } = Guid.Empty;
    public Guid? UserId { get; private set; }
    public string? UserRole { get; private set; }

    public void SetTenant(Guid tenantId, Guid? userId = null, string? role = null)
    {
        TenantId = tenantId;
        UserId = userId;
        UserRole = role;
    }
}

public static class PasswordHasher
{
    public static string HashPassword(string password)
    {
        using var sha = SHA512.Create();
        var salt = Guid.NewGuid().ToString("N");
        var bytes = Encoding.UTF8.GetBytes(password + ":" + salt);
        var hash = Convert.ToBase64String(sha.ComputeHash(bytes));
        return $"{salt}:{hash}";
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash) || !storedHash.Contains(':'))
            return false;

        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;
        var salt = parts[0];
        var hash = parts[1];

        using var sha = SHA512.Create();
        var bytes = Encoding.UTF8.GetBytes(password + ":" + salt);
        var computed = Convert.ToBase64String(sha.ComputeHash(bytes));
        return computed == hash;
    }
}

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;

    public JwtTokenGenerator(string? secretKey = null, string? issuer = null, string? audience = null)
    {
        _secretKey = secretKey ?? "BunnyOS_Super_Secret_Production_Key_2026_Bangladesh_Dhaka_Secure!";
        _issuer = issuer ?? "BunnyOS.Api";
        _audience = audience ?? "BunnyOS.Client";
    }

    public string GenerateToken(User user, Tenant tenant)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_secretKey);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("tenant_id", tenant.Id.ToString()),
            new Claim("business_name", tenant.Name),
            new Claim("currency_symbol", tenant.CurrencySymbol)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(30),
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
