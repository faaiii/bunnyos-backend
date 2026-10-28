using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using BunnyOS.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BunnyOS.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, string purpose)
    {
        var subject = $"🐰 BunnyOS Security Code: {otpCode}";
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; background-color: #f4f6fb; margin: 0; padding: 20px; }}
        .container {{ max-width: 540px; margin: 0 auto; background: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.06); }}
        .header {{ background: linear-gradient(135deg, #7C4DFF, #512DA8); color: white; padding: 30px; text-align: center; }}
        .header h1 {{ margin: 0; font-size: 24px; font-weight: 700; }}
        .header p {{ margin: 8px 0 0; font-size: 14px; opacity: 0.9; }}
        .content {{ padding: 30px; text-align: center; }}
        .purpose-badge {{ display: inline-block; background: #EDE7F6; color: #512DA8; padding: 6px 14px; border-radius: 20px; font-size: 13px; font-weight: 600; margin-bottom: 20px; }}
        .otp-box {{ background: #F8F9FE; border: 2px dashed #7C4DFF; border-radius: 12px; padding: 20px; margin: 20px 0; }}
        .otp-code {{ font-size: 36px; font-weight: 800; letter-spacing: 8px; color: #7C4DFF; margin: 0; }}
        .notice {{ color: #64748B; font-size: 13px; line-height: 1.6; margin-top: 20px; }}
        .warning {{ color: #EF4444; font-size: 12px; margin-top: 15px; font-weight: 600; }}
        .footer {{ background: #FAF5FF; padding: 20px; text-align: center; font-size: 12px; color: #94A3B8; border-top: 1px solid #E2E8F0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🐰 BunnyOS Security Verification</h1>
            <p>স্মার্ট ব্যবসা, নিরাপদ ব্যবস্থাপনা</p>
        </div>
        <div class='content'>
            <div class='purpose-badge'>{purpose}</div>
            <p style='color: #1E293B; font-size: 15px; margin: 0;'>আপনার BunnyOS অ্যাকাউন্টের ভেরিফিকেশন কোড নিচে দেওয়া হলো:</p>
            <div class='otp-box'>
                <div class='otp-code'>{otpCode}</div>
            </div>
            <p class='notice'>এই কোডটি আগামী <strong>৫ মিনিটের</strong> জন্য কার্যকর থাকবে। এটি কারো সাথে শেয়ার করবেন না।</p>
            <p class='warning'>⚠️ আপনি যদি এই কোডটির অনুরোধ না করে থাকেন, তবে অবিলম্বে আপনার পাসওয়ার্ড পরিবর্তন করুন।</p>
        </div>
        <div class='footer'>
            BunnyOS Business Platform &copy; {DateTime.UtcNow.Year} | ঢাকা, বাংলাদেশ।
        </div>
    </div>
</body>
</html>";

        return await SendCustomEmailAsync(toEmail, subject, htmlBody);
    }

    public async Task<bool> SendCustomEmailAsync(string toEmail, string subject, string htmlBody)
    {
        bool.TryParse(_config["EmailSettings:IsLive"], out var isLive);
        var host = _config["EmailSettings:SmtpHost"] ?? "smtp.gmail.com";
        int.TryParse(_config["EmailSettings:SmtpPort"], out var port);
        if (port <= 0) port = 587;
        var senderEmail = _config["EmailSettings:SenderEmail"] ?? "bdfaisal971@gmail.com";
        var senderName = _config["EmailSettings:SenderName"] ?? "BunnyOS Security";
        var username = _config["EmailSettings:SmtpUsername"] ?? senderEmail;
        var password = _config["EmailSettings:SmtpPassword"] ?? string.Empty;
        var sslRaw = _config["EmailSettings:EnableSsl"];
        var enableSsl = string.IsNullOrWhiteSpace(sslRaw) || !bool.TryParse(sslRaw, out var parsedSsl) || parsedSsl;

        if (!isLive || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogInformation("[EmailService (Dev Mode)] Target: {ToEmail} | Subject: {Subject} | Note: Email logged (Configure EmailSettings:SmtpPassword in appsettings.json for live sending)", toEmail, subject);
            Console.WriteLine($"[EMAIL-DEV] To: {toEmail} | Subject: {subject}");
            return true;
        }

        try
        {
            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = enableSsl,
                Timeout = 4000
            };

            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
            _logger.LogInformation("[EmailService] Live email successfully sent to {ToEmail}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EmailService Error] Failed to send email to {ToEmail}: {Message}", toEmail, ex.Message);
            Console.WriteLine($"[EMAIL-ERROR] Failed to send to {toEmail}: {ex.Message}");
            return false;
        }
    }
}
