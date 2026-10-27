using System;
using BunnyOS.Domain.Common;

namespace BunnyOS.Domain.Entities;

public class SubscriptionPlan : BaseEntity
{
    public string Name { get; set; } = string.Empty; // Free, Pro, Business, Enterprise
    public decimal Price { get; set; }
    public string Interval { get; set; } = "Monthly"; // Monthly, Quarterly, Yearly
    public string Features { get; set; } = string.Empty; // Comma-separated or description
    public bool IsActive { get; set; } = true;
    public int MaxProducts { get; set; } = 100;
    public int MaxStaff { get; set; } = 2;
    public bool AiAssistantEnabled { get; set; } = true;
}
