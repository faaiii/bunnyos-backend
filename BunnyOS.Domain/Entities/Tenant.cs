using System;
using System.Collections.Generic;
using BunnyOS.Domain.Common;

namespace BunnyOS.Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Tagline { get; set; } = "স্মার্ট ব্যবসা, সহজ ব্যবস্থাপনা";
    public string Currency { get; set; } = "BDT";
    public string CurrencySymbol { get; set; } = "৳";
    public string Timezone { get; set; } = "Asia/Dhaka";
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
    public ICollection<Supplier> Suppliers { get; set; } = new List<Supplier>();
}

public enum UserRole
{
    PlatformOwner = 0,
    Owner = 1,
    Manager = 2,
    Cashier = 3,
    InventoryStaff = 4
}

public class User : TenantEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Owner;
    public bool IsActive { get; set; } = true;
    public bool IsVerified { get; set; } = false;
    public string? VerificationCode { get; set; }
    public DateTime? VerificationExpiresAtUtc { get; set; }
    public string SubscriptionPlan { get; set; } = "Free";
    public string SubscriptionStatus { get; set; } = "Active";
    public DateTime? SubscriptionExpiresAtUtc { get; set; }
    public string AppVersion { get; set; } = "v1.0.0";
    public DateTime? LastLoginAtUtc { get; set; }

    public Tenant? Tenant { get; set; }
}
