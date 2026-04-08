using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Services.Implementation
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notificationRepo;
        private readonly IEmailService _emailService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<NotificationService> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public NotificationService(
            INotificationRepository notificationRepo,
            IEmailService emailService,
            UserManager<ApplicationUser> userManager,
            ILogger<NotificationService> logger,
            IUnitOfWork unitOfWork)
        {
            _notificationRepo = notificationRepo;
            _emailService = emailService;
            _userManager = userManager;
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        public async Task SendAsync(int userId, string title, string content, NotificationType type)
        {
            // 1. Lưu notification vào DB (in-app) — giữ nguyên logic cũ
            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Content = content,
                Type = type,
                IsRead = false,
                CreatedAt = DateTimeHelper.VietnamNow()
            };
            _notificationRepo.Add(notification);
            await _unitOfWork.CompleteAsync();

            // 2. Gửi email CC — nếu user có email đã xác thực
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user != null && !string.IsNullOrEmpty(user.Email) && user.EmailConfirmed)
                {
                    var typeLabel = type.ToString();
                    var emailBody = $@"
                        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                            <div style='background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%); padding: 20px; border-radius: 12px 12px 0 0;'>
                                <h2 style='color: white; margin: 0;'>🔔 {title}</h2>
                            </div>
                            <div style='background: #f9fafb; padding: 24px; border: 1px solid #e5e7eb; border-top: none; border-radius: 0 0 12px 12px;'>
                                <p style='color: #374151; font-size: 15px; line-height: 1.6;'>{content}</p>
                                <div style='margin-top: 16px; padding: 12px; background: #eff6ff; border-radius: 8px;'>
                                    <small style='color: #6b7280;'>📌 Loại: {typeLabel} | ⏰ {DateTimeHelper.VietnamNow():dd/MM/yyyy HH:mm}</small>
                                </div>
                            </div>
                            <p style='color: #9ca3af; font-size: 11px; text-align: center; margin-top: 16px;'>
                                © ChargeSlot - Hệ thống đặt chỗ sạc xe điện
                            </p>
                        </div>";

                    await _emailService.SendEmailAsync(
                        to: user.Email,
                        subject: $"[ChargeSlot] {title}",
                        body: emailBody
                    );
                }
            }
            catch (Exception ex)
            {
                // Email fail KHÔNG block notification — chỉ log warning
                _logger.LogWarning(ex, "[Notification] Failed to send email CC to user {UserId}", userId);
            }
        }
    }
}


