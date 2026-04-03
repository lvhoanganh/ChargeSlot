using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Analytics;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace ChargeSlot.Api.Services.Implementation
{
    public class AiChatbotService : IAiChatbotService
    {
        private readonly HttpClient _httpClient;
        private readonly ChargeSlotDbContext _db;
        private readonly ILogger<AiChatbotService> _logger;
        private readonly IWalletService _walletService;
        private readonly IChargingStationService _stationService;
        private readonly IConfiguration _configuration;

        public AiChatbotService(
            HttpClient httpClient, 
            ChargeSlotDbContext db, 
            ILogger<AiChatbotService> logger,
            IWalletService walletService,
            IChargingStationService stationService,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _db = db;
            _logger = logger;
            _walletService = walletService;
            _stationService = stationService;
            _configuration = configuration;
        }

        private (string ApiKey, string Model) GetGeminiConfig()
        {
            var apiKey = _configuration["GeminiApi:ApiKey"]?.Trim() 
                ?? throw new InvalidOperationException("GeminiApi:ApiKey is not configured.");
            var model = _configuration["GeminiApi:Model"]?.Trim() ?? "gemini-2.5-flash";
            return (apiKey, model);
        }

        public async Task<ChatbotResponseDto> ProcessDriverChatAsync(int userId, ChatbotRequestDto request)
        {
            // 1. Chuẩn bị bối cảnh (System Prompt) cho Driver
            string systemPrompt = @"Bạn là Trợ lý AI trên xe điện của nền tảng ChargeSlot, được thiết kế để hỗ trợ Tài xế.
Bạn BẮT BUỘC phải xưng là 'Tôi' và gọi tài xế là 'Anh/Chị'.
Nhiệm vụ: Cung cấp thông tin số dư ví, lịch sử giao dịch và tìm trạm sạc.
Quy tắc Lõi: 
1. Nếu tài xế hỏi số dư tiền, HÃY DÙNG CHỨC NĂNG check_wallet_balance.
2. Nếu tài xế hỏi tìm trạm sạc, HÃY DÙNG CHỨC NĂNG find_nearby_stations.";

            // 2. Định nghĩa Kho vũ khí (Tools) cho Driver
            var tools = new[]
            {
                new 
                {
                    functionDeclarations = new[]
                    {
                        new {
                            name = "check_wallet_balance",
                            description = "Truy xuất số dư thực tế trong ví của tài khoản tài xế hiện tại."
                        },
                        new {
                            name = "find_nearby_stations",
                            description = "Hiển thị danh sách 5 trạm sạc đang hoạt động và được kiểm duyệt gần nhất."
                        }
                    }
                }
            };

            // 3. Thực thi vòng lặp Giao tiếp AI
            return await ExecuteAiToolLoopAsync(userId, request, systemPrompt, tools, "Driver");
        }

        public async Task<ChatbotResponseDto> ProcessOwnerChatAsync(int ownerId, ChatbotRequestDto request)
        {
            return new ChatbotResponseDto { ReplyMarkdown = "Trợ lý Chủ trạm đang được cập nhật." };
        }

        public async Task<ChatbotResponseDto> ProcessAdminChatAsync(ChatbotRequestDto request)
        {
            return new ChatbotResponseDto { ReplyMarkdown = "Trợ lý Admin đang được cập nhật." };
        }

        /// <summary>
        /// Vòng lặp đệ quy nhúng Function Calling (Core Engine)
        /// Chống Infinite Loop bằng maxToolCalls (Giới hạn gọi hàm).
        /// </summary>
        private async Task<ChatbotResponseDto> ExecuteAiToolLoopAsync(
            int userId, 
            ChatbotRequestDto request, 
            string systemPrompt, 
            object tools,
            string role)
        {
            var (apiKey, model) = GetGeminiConfig();
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            // Xây dựng lịch sử trò chuyện
            var contents = new List<object>
            {
                new { role = "user", parts = new[] { new { text = $"[HƯỚNG DẪN HỆ THỐNG ĐƯỢC ẨN: {systemPrompt}]\n\nBây giờ hãy trả lời tin nhắn sau của tôi:" } } },
                new { role = "model", parts = new[] { new { text = "Đã rõ. Tôi sẽ tuân thủ nghiêm ngặt." } } }
            };

            // GIỚI HẠN LỊCH SỬ CHAT TRÁNH TẤN CÔNG TOKEN DDOS VÀ TIẾT KIỆM BĂNG THÔNG
            foreach (var msg in request.History.TakeLast(6))
            {
                contents.Add(new { role = msg.Role, parts = new[] { new { text = msg.Content } } });
            }
            contents.Add(new { role = "user", parts = new[] { new { text = request.CurrentMessage } } });

            int maxToolCalls = 3;
            int currentToolCalls = 0;

            while (currentToolCalls < maxToolCalls)
            {
                var payload = new
                {
                    contents,
                    tools,
                    generationConfig = new { temperature = 0.5, maxOutputTokens = 800, thinkingConfig = new { thinkingBudget = 0 } }
                };

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
                requestMessage.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                // GIỚI HẠN THỜI GIAN KẾT NỐI (TIMEOUT) CHỐNG TREO LUỒNG C#
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(requestMessage, cts.Token);
                }
                catch (TaskCanceledException)
                {
                    return new ChatbotResponseDto { ReplyMarkdown = "⚠️ Gián đoạn kết nối đến Trung tâm dữ liệu (Timeout 30s). Vui lòng thử lại lúc khác." };
                }

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Gemini Chat Error ({StatusCode}): {Err}", response.StatusCode, err);
                    return new ChatbotResponseDto { ReplyMarkdown = $"⚠️ Lỗi kết nối AI ({response.StatusCode}). Vui lòng thử lại." };
                }

                var responseString = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(responseString);
                var candidates = document.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() == 0)
                    break;

                var candidate = candidates[0];
                var parts = candidate.GetProperty("content").GetProperty("parts");

                // KIỂM TRA XEM AI CÓ YÊU CẦU GỌI HÀM (FUNCTION CALL) HAY KHÔNG
                bool hasFunctionCall = false;
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("functionCall", out var functionCall))
                    {
                        hasFunctionCall = true;
                        currentToolCalls++;
                        string functionName = functionCall.GetProperty("name").GetString()!;
                        
                        // THỰC THI TOOL THEO TÊN (Hàng rào bảo mật 100%)
                        object resultData = null;
                        
                        if (role == "Driver")
                        {
                            switch (functionName)
                            {
                                case "check_wallet_balance":
                                    var wallet = await _walletService.GetOrCreateWalletAsync(userId);
                                    resultData = new { message = "Lấy số dư thành công", balance = wallet.AvailableBalance, frozen = wallet.FrozenBalance };
                                    break;
                                case "find_nearby_stations":
                                    // Mock fetching stations for safe demo
                                    resultData = new { stations = new[] { "Trạm EcoGreen Quận 7", "Trạm Vinhome Tân Cảng" } };
                                    break;
                                default:
                                    resultData = new { error = "Quyền truy cập hàm bị từ chối do bảo mật." };
                                    break;
                            }
                        }
                        else 
                        {
                            resultData = new { error = "Tool mapping not implemented for this role." };
                        }

                        // Ghi lại bước AI gọi hàm để nhúng vào lịch sử
                        contents.Add(new { role = "model", parts = new[] { new { functionCall = new { name = functionName, args = new { } } } } });
                        
                        // Đẩy kết quả C# về lại cho AI
                        contents.Add(new { role = "user", parts = new[] { new { functionResponse = new { name = functionName, response = resultData } } } });
                    }
                }

                // Nếu AI không gọi hàm nữa, nghĩa là nó đã nhả ra TEXT cuối cùng
                if (!hasFunctionCall)
                {
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var textProp))
                        {
                            response.Dispose();
                            return new ChatbotResponseDto { ReplyMarkdown = textProp.GetString() };
                        }
                    }
                }
                response.Dispose();
            }

            return new ChatbotResponseDto { ReplyMarkdown = "Xin lỗi, tôi phải suy nghĩ quá lâu (vượt ngưỡng an toàn) nên tự động ngắt kết nối." };
        }
    }
}
