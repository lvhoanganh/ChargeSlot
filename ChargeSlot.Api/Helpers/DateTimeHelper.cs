namespace ChargeSlot.Api.Helpers
{
    public static class DateTimeHelper
    {
        private static readonly TimeZoneInfo VietnamTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

        /// <summary>
        /// Lấy thời gian hiện tại theo múi giờ Việt Nam (UTC+7).
        /// Trả về DateTime với Kind = Local để tránh lỗi so sánh với UTC.
        /// </summary>
        public static DateTime VietnamNow()
        {
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
            return DateTime.SpecifyKind(now, DateTimeKind.Local);
        }
    }
}
