using ChargeSlot.Api.Constants;
using ChargeSlot.Api.Data;
using ChargeSlot.Api.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ChargeSlot.Api.Middlewares
{
    public class SecurityBanCheckMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityBanCheckMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ChargeSlotDbContext dbContext)
        {
            // Nếu người dùng có gửi token và được xác nhận là hợp lệ
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userIdStr = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out int userId))
                {
                    // Lấy Status trực tiếp từ DB (có thể nâng cấp lên Redis Cache nếu muốn tối ưu hệ thống lớn)
                    var user = await dbContext.Users
                        .AsNoTracking() // Dùng AsNoTracking cho nhanh vì chỉ để check
                        .Where(u => u.Id == userId)
                        .Select(u => new { u.Status, u.BannedUntil })
                        .FirstOrDefaultAsync();

                    if (user != null)
                    {
                        bool isBanned = false;
                        string reason = "";

                        // Kiểm tra status vĩnh viễn
                        if (user.Status == UserStatusConstants.Banned)
                        {
                            isBanned = true;
                            reason = "Tài khoản của bạn đã bị khóa vĩnh viễn do vi phạm nghiêm trọng.";
                        }
                        // Kiểm tra auto-ban (Suspended)
                        else if (user.Status == UserStatusConstants.Suspended && user.BannedUntil != null)
                        {
                            if (user.BannedUntil > DateTimeHelper.VietnamNow())
                            {
                                isBanned = true;
                                reason = $"Tài khoản của bạn đang bị đình chỉ hoạt động cho đến ngày {user.BannedUntil.Value:dd/MM/yyyy}.";
                            }
                        }

                        if (isBanned)
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsJsonAsync(new 
                            { 
                                message = reason, 
                                isBanned = true 
                            });
                            return; // Chặn ngang Request, không cho đi tiếp vào Controller
                        }
                    }
                }
            }

            await _next(context);
        }
    }
}
