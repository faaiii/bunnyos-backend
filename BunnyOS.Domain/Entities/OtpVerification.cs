using System;
using BunnyOS.Domain.Common;

namespace BunnyOS.Domain.Entities;

public class OtpVerification : BaseEntity
{
    public string TargetEmail { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Purpose { get; set; } = "AccountVerification"; // AccountVerification, OwnerLogin, PasswordReset
    public DateTime ExpiresAtUtc { get; set; }
    public bool IsUsed { get; set; } = false;
    public int AttemptCount { get; set; } = 0;
}
