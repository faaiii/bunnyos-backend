using System;
using BunnyOS.Domain.Common;

namespace BunnyOS.Domain.Entities;

public class PlatformNotification : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Category { get; set; } = "System"; // System, Subscriptions, AI, Security
    public string Severity { get; set; } = "Info"; // Info, Warning, Error, Success
    public bool IsRead { get; set; } = false;
    public string? Metadata { get; set; }
}
