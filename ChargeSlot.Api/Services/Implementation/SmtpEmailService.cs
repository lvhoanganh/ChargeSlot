using System.Net;
using System.Net.Mail;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ChargeSlot.Api.Services.Implementation
{
    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var emailConfig = _configuration.GetSection("Email");
            
            var host = emailConfig["SmtpHost"];
            var portStr = emailConfig["SmtpPort"];
            var username = emailConfig["Username"];
            var password = emailConfig["Password"];
            var from = emailConfig["From"];

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                _logger.LogWarning("Email sending failed because SMTP config is missing in appsettings.json or Environment Variables.");
                return;
            }

            if (!int.TryParse(portStr, out int port)) port = 587;

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(from ?? username, "ChargeSlot System"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            mailMessage.To.Add(to);

            try
            {
                await client.SendMailAsync(mailMessage);
                _logger.LogInformation($"[SMTP] Đã gửi thông báo thật qua Email tới: {to}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[SMTP] Gửi Email tới {to} thất bại.");
                throw; // Rethrow to let the caller handle or return 500
            }
        }
    }
}
