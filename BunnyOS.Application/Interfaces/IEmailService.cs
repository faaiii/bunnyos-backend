using System.Threading.Tasks;

namespace BunnyOS.Application.Interfaces;

public interface IEmailService
{
    Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, string purpose);
    Task<bool> SendCustomEmailAsync(string toEmail, string subject, string htmlBody);
}
