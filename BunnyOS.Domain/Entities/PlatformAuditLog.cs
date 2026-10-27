using System;
using BunnyOS.Domain.Common;

namespace BunnyOS.Domain.Entities;

public class PlatformAuditLog : BaseEntity
{
    public string ActorEmail { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // UserSuspended, UserRestored, SubscriptionUpdated, SessionRevoked, PlanCreated
    public string TargetResource { get; set; } = string.Empty;
    public string Result { get; set; } = "Success";
    public string Details { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
}
