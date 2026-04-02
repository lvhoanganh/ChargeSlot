using ChargeSlot.Api.Services.Interfaces;

namespace ChargeSlot.Api.Services.Implementation
{
    public class MockEmailService : IEmailService
    {
        private readonly ILogger<MockEmailService> _logger;

        public MockEmailService(ILogger<MockEmailService> logger)
        {
            _logger = logger;
        }

        public Task SendEmailAsync(string to, string subject, string body)
        {
            // Trong môi trường production thật, sếp sẽ tích hợp SMTP (SendGrid/Gmail) vào đây.
            // Hiện tại ta in ra Log để Sếp lấy mã OTP trong Terminal
            _logger.LogInformation("================================================");
            _logger.LogInformation($"📨 GỬI EMAIL THÀNH CÔNG ĐẾN: {to}");
            _logger.LogInformation($"📌 Chủ đề: {subject}");
            _logger.LogInformation($"💡 Nội dung: {body}");
            _logger.LogInformation("================================================");
            
            return Task.CompletedTask;
        }
    }
}
