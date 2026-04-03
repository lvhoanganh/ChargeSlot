using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Analytics;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Google.Apis.Auth.OAuth2;

namespace ChargeSlot.Api.Services.Implementation
{
    public class GeminiInsightsService : IAiInsightsService
    {
        private readonly HttpClient _httpClient;
        private readonly ChargeSlotDbContext _db;
        private readonly ILogger<GeminiInsightsService> _logger;

        public GeminiInsightsService(HttpClient httpClient, ChargeSlotDbContext db, ILogger<GeminiInsightsService> logger)
        {
            _httpClient = httpClient;
            _db = db;
            _logger = logger;
        }

        private async Task<(string ProjectId, string Region)> GetVertexConfigAsync()
        {
            var pConfig = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "VertexProjectId");
            var rConfig = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "VertexRegion");
            
            // Theo như User xác nhận: chargeslot-42b86 và us-central1
            var projectId = pConfig?.Value?.Trim() ?? "chargeslot-42b86";
            var region = rConfig?.Value?.Trim() ?? "us-central1";

            return (projectId, region);
        }

        public async Task<AiInsightResponseDto> GenerateAdminInsightAsync(AdminDashboardMetricsDto metrics)
        {
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

            var insight = await CallVertexApiAsync(prompt);
            return new AiInsightResponseDto { InsightMarkdown = insight };
        }

        public async Task<AiInsightResponseDto> GenerateOwnerInsightAsync(OwnerDashboardMetricsDto metrics)
        {
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

            var insight = await CallVertexApiAsync(prompt);
            return new AiInsightResponseDto { InsightMarkdown = insight };
        }

        private async Task<string> CallVertexApiAsync(string prompt)
        {
            var credFilePath = Path.Combine(Directory.GetCurrentDirectory(), "vertex-credentials.json");
            
            if (!File.Exists(credFilePath))
            {
                return "⚠️ **Lỗi Hệ Thống:** Không tìm thấy tệp xác thực `vertex-credentials.json` của Vertex AI trên thư mục gốc dự án C#.";
            }

            var (projectId, region) = await GetVertexConfigAsync();
            var url = $"https://{region}-aiplatform.googleapis.com/v1/projects/{projectId}/locations/{region}/publishers/google/models/gemini-2.0-flash:generateContent";

            try 
            {
                // Sinh Bearer Token bảo mật bằng OAuth2
                var credential = GoogleCredential.FromFile(credFilePath)
                    .CreateScoped("https://www.googleapis.com/auth/cloud-platform");
                
                string token = await ((ITokenAccess)credential).GetAccessTokenForRequestAsync();

                var payload = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = prompt } } }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        maxOutputTokens = 1024
                    }
                };

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                requestMessage.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(requestMessage);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Vertex API Error: {StatusCode} - {Body}", response.StatusCode, errorBody);
                    try 
                    {
                        using var errDoc = JsonDocument.Parse(errorBody);
                        var errMsg = errDoc.RootElement.GetProperty("error").GetProperty("message").GetString();
                        return $"⚠️ **Lỗi từ Vertex AI ({response.StatusCode})**: {errMsg}";
                    }
                    catch
                    {
                        return $"⚠️ Lỗi từ Vertex AI (Mã {response.StatusCode}): {errorBody}";
                    }
                }

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
                _logger.LogError(ex, "Failed to call Vertex API");
                return $"Đã xảy ra lỗi khi xác thực Token Vertex. Chi tiết: {ex.Message}";
            }
        }
    }
}
