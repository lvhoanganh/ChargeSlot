using System.Text;
using System.Text.Json;
using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Analytics;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Services.Implementation
{
    public class GeminiInsightsService : IAiInsightsService
    {
        private readonly HttpClient _httpClient;
        private readonly ChargeSlotDbContext _db;
        private readonly ILogger<GeminiInsightsService> _logger;
        private const string GEMINI_API_URL = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={0}";

        public GeminiInsightsService(HttpClient httpClient, ChargeSlotDbContext db, ILogger<GeminiInsightsService> logger)
        {
            _httpClient = httpClient;
            _db = db;
            _logger = logger;
        }

        private async Task<string> GetApiKeyAsync()
        {
            // Or get from IConfiguration
            var config = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "GeminiApiKey");
            if (config == null || string.IsNullOrWhiteSpace(config.Value))
            {
                // MOCK response if no key is configured to avoid crashes during testing
                return string.Empty;
            }
            return config.Value.Trim();
        }

        public async Task<AiInsightResponseDto> GenerateAdminInsightAsync(AdminDashboardMetricsDto metrics)
        {
            var apiKey = await GetApiKeyAsync();
            if (string.IsNullOrEmpty(apiKey))
            {
                return new AiInsightResponseDto
                {
                    InsightMarkdown = "⚠️ **Thiếu API Key:** Vui lòng cấu hình `GeminiApiKey` trong bảng `SystemConfigs` để kích hoạt Trợ lý AI."
                };
            }

            var prompt = $@"
Đóng vai: Giám đốc Kiểm soát Hệ thống & Phân tích Dữ liệu (Chief Data Officer) của nền tảng trạm sạc ChargeSlot (Admin Mode).
Phong cách viết: Chuyên nghiệp, đanh thép, tập trung vào con số, đi thẳng vào rủi ro, không lan man. Sử dụng định dạng Markdown nổi bật (Heading, in đậm, emoji).
Dung lượng: Tối đa 250 - 300 chữ. Báo cáo phải thật súc tích để đọc nhanh trên Dashboard.

DỮ LIỆU KINH DOANH (30 ngày qua):
- Doanh thu Sàn (Nền tảng giữ lại): {metrics.TotalPlatformRevenue:N0} VNĐ
- Dòng tiền cọc (Escrow) đang giam: {metrics.TotalEscrowBalance:N0} VNĐ
- Tổng Trạm: {metrics.TotalStations} (Đang hoạt động: {metrics.TotalActiveStations})
- Tổng User: {metrics.TotalUsers}
- Tổng Đơn: {metrics.BookingsLast30Days} | Tỉ lệ hủy đơn: {metrics.CancelRateLast30Days * 100:0.##}%
- Số lượt khiếu nại (Disputes): {metrics.DisputesLast30Days}

DANH SÁCH ĐEN (CẦN PHÂN TÍCH RỦI RO):
- Trạm dính khiếu nại nhiều nhất: {(metrics.TopDisputedStations.Any() ? string.Join(", ", metrics.TopDisputedStations.Select(s => $"{s.StationName} ({s.DisputeCount} lần)")) : "Không có")}
- Tài xế có dấu hiệu hủy đơn/bùng kèo bất thường:
{string.Join("\n", metrics.HighRiskDrivers.Select(d => $"- {d.DriverName}: {d.SuspiciousNote}"))}

NHIỆM VỤ CỦA BẠN: Viết báo cáo nội bộ chia làm 4 phần rõ ràng:
1. 💰 [Dòng Tiền & Tăng Trưởng] Nhận định tình trạng lưu thông tài chính.
2. ⚡ [Vận Hành Nền Tảng] Bình luận về lượng Booking và biểu đồ Hủy đơn.
3. ⚠️ [Báo Động Đỏ] Gõ đầu đích danh (bằng chữ in đậm) các tài khoản/trạm sạc lạm dụng ở danh sách đen. Nêu rõ động cơ gian lận của chúng.
4. 💡 [Khuyến Nghị Cấp Thiết] Đề xuất 2 hành động cụ thể cực gắt (Ví dụ: Ban vĩnh viễn, giữ tiền Escrow, gọi điện xác minh).
Yêu cầu bắt buộc: Chỉ in ra Markdown, tuyệt đối không có lời mở đầu hay kết luận sáo rỗng.
";

            var insight = await CallGeminiApiAsync(apiKey, prompt);
            return new AiInsightResponseDto { InsightMarkdown = insight };
        }

        public async Task<AiInsightResponseDto> GenerateOwnerInsightAsync(OwnerDashboardMetricsDto metrics)
        {
            var apiKey = await GetApiKeyAsync();
            if (string.IsNullOrEmpty(apiKey))
            {
                return new AiInsightResponseDto
                {
                    InsightMarkdown = "⚠️ **Thiếu API Key:** Vui lòng liên hệ Admin cấu hình `GeminiApiKey` để mở khóa Cố Vấn Doanh Thu AI cho Chủ Trạm."
                };
            }

            var prompt = $@"
Đóng vai: Chuyên gia Khai vấn Kinh doanh (Business Coach) xuất sắc nhất khu vực, chuyên tư vấn tăng doanh thu cho Chủ Trạm Sạc Xe Điện trên ChargeSlot.
Phong cách viết: Cực kỳ vồ vập, năng lượng cao, nhiệt huyết, xưng 'Tôi' và gọi chủ trạm là 'Sếp'. Dùng nhiều câu cảm thán khích lệ, nhét emoji vào mỗi ý.
Dung lượng: Tối đa 250 chữ. Viết theo kiểu báo cáo nhanh gửi qua tin nhắn.

DỮ LIỆU KINH DOANH (30 NGÀY QUA) CỦA SẾP:
- Doanh thu ròng: {metrics.RevenueLast30Days:N0} VNĐ | Số dư ví: {metrics.WalletBalance:N0} VNĐ
- Sở hữu: {metrics.TotalStations} Trạm sạc 
- Giao dịch: {metrics.BookingsLast30Days} đơn | Tỉ lệ rớt khách (Hủy): {metrics.CancelRateLast30Days * 100:0.##}%
- Doanh thu Dịch vụ phụ (Nước uống, đồ ăn...): {(metrics.TopServicesSold.Any() ? string.Join(", ", metrics.TopServicesSold.Select(s => $"{s.ServiceName} ({s.QuantitySold} món - {s.Revenue:N0}đ)")) : "Chưa bán được gì hoặc chưa setup")}
- Hiệu suất theo Trạm: {string.Join(" | ", metrics.StationPerformances.Select(s => $"{s.StationName} (Thu {s.TotalRevenue:N0}đ, {s.AverageRating}/5⭐)"))}

NHIỆM VỤ CỦA BẠN: Viết báo cáo gửi sếp chia làm 4 phần giật tít:
1. 🎯 [Báo Cáo Tiền Về] Bình luận về tốc độ kiếm tiền và vinh danh trạm sạc Gánh Team mạnh nhất.
2. 🕵️‍♂️ [Lỗ Hổng Khách Rơi] Phân tích lý do vì sao tỉ lệ hủy lại như vậy, rating có đang báo động không.
3. ☕ [Bẫy Dịch Vụ Phụ] Phân tích mảng Đồ ăn/Nước/DV phụ. Nếu món nào bán chạy, xúi sếp nhập thêm. Nếu chán, xúi sếp bổ sung Menu.
4. 🚀 [Chiến Lược Tối Ưu Tuần Tới] Bày cho sếp 1 mẹo xả giá Off-peak vào giờ vắng hoặc gộp Combo để kích Sale.
Yêu cầu bắt buộc: Chỉ in ra Markdown, không giải thích. Tiêu đề phải bùng nổ, tạo cảm giác 'wow' cho người đọc.
";

            var insight = await CallGeminiApiAsync(apiKey, prompt);
            return new AiInsightResponseDto { InsightMarkdown = insight };
        }

        private async Task<string> CallGeminiApiAsync(string apiKey, string prompt)
        {
            var url = string.Format(GEMINI_API_URL, apiKey);

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text = prompt } }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.7,
                    maxOutputTokens = 1024
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Google API Error: {StatusCode} - {Body}", response.StatusCode, errorBody);
                    return $"⚠️ Lỗi từ Google AI (Mã {response.StatusCode}): {errorBody}";
                }
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(responseString);

                var generatedText = document.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return generatedText ?? "Không thể tạo báo cáo lúc này.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call Gemini API");
                return $"Đã xảy ra lỗi khi kết nối với AI Studio. Lỗi: {ex.Message}";
            }
        }
    }
}
