using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Analytics;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text;

namespace ChargeSlot.Api.Services.Implementation
{
    public class AiChatbotService : IAiChatbotService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AiChatbotService> _logger;
        private readonly IWalletService _walletService;
        private readonly IChargingStationService _stationService;
        private readonly IChargingStationRepository _stationRepo;
        private readonly IConfiguration _configuration;
        private readonly IDashboardService _dashboardService;
        private readonly IBookingService _bookingService;
        private readonly IDisputeService _disputeService;

        public AiChatbotService(
            HttpClient httpClient, 
            ILogger<AiChatbotService> logger,
            IWalletService walletService,
            IChargingStationService stationService,
            IChargingStationRepository stationRepo,
            IConfiguration configuration,
            IDashboardService dashboardService,
            IBookingService bookingService,
            IDisputeService disputeService)
        {
            _httpClient = httpClient;
            _logger = logger;
            _walletService = walletService;
            _stationService = stationService;
            _stationRepo = stationRepo;
            _configuration = configuration;
            _dashboardService = dashboardService;
            _bookingService = bookingService;
            _disputeService = disputeService;
        }

        private (string ApiKey, string Model) GetGeminiConfig()
        {
            var apiKey = _configuration["GeminiApi:ApiKey"]?.Trim() 
                ?? throw new InvalidOperationException("GeminiApi:ApiKey is not configured.");
            var model = _configuration["GeminiApi:Model"]?.Trim() ?? "gemini-2.5-flash";
            return (apiKey, model);
        }

        // ============================================================
        // DRIVER CHATBOT
        // ============================================================
        public async Task<ChatbotResponseDto> ProcessDriverChatAsync(int userId, ChatbotRequestDto request)
        {
            string systemPrompt = @"Bạn là Trợ lý AI trên xe điện của nền tảng ChargeSlot, được thiết kế để hỗ trợ Tài xế.
Bạn BẮT BUỘC phải xưng là 'Tôi' và gọi tài xế là 'Anh/Chị'.
Nhiệm vụ: Cung cấp thông tin số dư ví, lịch sử giao dịch và tìm trạm sạc.
Quy tắc Lõi: 
1. Nếu tài xế hỏi số dư tiền, HÃY DÙNG CHỨC NĂNG check_wallet_balance.
2. Nếu tài xế hỏi tìm trạm sạc, HÃY DÙNG CHỨC NĂNG find_nearby_stations.";

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

            return await ExecuteAiToolLoopAsync(userId, request, systemPrompt, tools, "Driver");
        }

        // ============================================================
        // OWNER CHATBOT
        // ============================================================
        public async Task<ChatbotResponseDto> ProcessOwnerChatAsync(int ownerId, ChatbotRequestDto request)
        {
            string systemPrompt = @"Bạn là Cố vấn AI trên nền tảng ChargeSlot, được thiết kế để hỗ trợ Chủ trạm sạc xe điện.
Bạn BẮT BUỘC phải xưng là 'Tôi' và gọi chủ trạm là 'Anh/Chị'.
Nhiệm vụ: Cung cấp thông tin phân tích doanh thu, báo cáo trạm sạc, số dư ví và tình hình đơn đặt.
Quy tắc Lõi:
1. Nếu chủ trạm hỏi số dư tiền/ví, HÃY DÙNG CHỨC NĂNG check_owner_wallet.
2. Nếu chủ trạm hỏi doanh thu/thống kê/báo cáo/rating/hiệu suất, HÃY DÙNG CHỨC NĂNG get_owner_dashboard.
3. Nếu chủ trạm hỏi danh sách đơn đặt/booking/khách hàng gần đây, HÃY DÙNG CHỨC NĂNG get_owner_bookings.
Lưu ý: Luôn trả lời bằng tiếng Việt, sử dụng format Markdown rõ ràng.";

            var tools = new[]
            {
                new
                {
                    functionDeclarations = new[]
                    {
                        new {
                            name = "check_owner_wallet",
                            description = "Truy xuất số dư thực tế trong ví của tài khoản chủ trạm hiện tại (available balance và frozen balance)."
                        },
                        new {
                            name = "get_owner_dashboard",
                            description = "Lấy tổng quan kinh doanh 30 ngày qua: doanh thu ròng, số booking, tỉ lệ hủy đơn, hiệu suất từng trạm (tên, doanh thu, rating), dịch vụ phụ bán chạy nhất."
                        },
                        new {
                            name = "get_owner_bookings",
                            description = "Lấy danh sách 10 đơn đặt gần nhất tại các trạm sạc của chủ trạm (ID, trạng thái, tên tài xế, tên trạm, thời gian, số tiền)."
                        }
                    }
                }
            };

            return await ExecuteAiToolLoopAsync(ownerId, request, systemPrompt, tools, "Owner");
        }

        // ============================================================
        // ADMIN CHATBOT
        // ============================================================
        public async Task<ChatbotResponseDto> ProcessAdminChatAsync(ChatbotRequestDto request)
        {
            string systemPrompt = @"Bạn là AI Giám sát hệ thống ChargeSlot, hỗ trợ Admin quản lý toàn bộ nền tảng.
Bạn xưng là 'Tôi' và gọi người dùng là 'Admin'.
Nhiệm vụ: Phân tích báo cáo toàn hệ thống, giám sát dòng tiền, quản lý rủi ro, theo dõi trạm sạc và khiếu nại.
Quy tắc Lõi:
1. Nếu Admin hỏi tổng quan hệ thống/doanh thu/user/escrow/platform revenue/thống kê, HÃY DÙNG CHỨC NĂNG get_admin_dashboard.
2. Nếu Admin hỏi trạm chờ duyệt/pending stations, HÃY DÙNG CHỨC NĂNG get_pending_stations.
3. Nếu Admin hỏi khiếu nại/dispute chưa xử lý, HÃY DÙNG CHỨC NĂNG get_pending_disputes.
Lưu ý: Luôn trả lời bằng tiếng Việt, sử dụng format Markdown rõ ràng. Phân tích rủi ro phải đanh thép, đi thẳng vào vấn đề.";

            var tools = new[]
            {
                new
                {
                    functionDeclarations = new[]
                    {
                        new {
                            name = "get_admin_dashboard",
                            description = "Lấy tổng quan hệ thống 30 ngày qua: Escrow balance, Platform Revenue, tổng users, tổng trạm (active/tổng), bookings, tỉ lệ hủy, disputes, top trạm dính khiếu nại, tài xế high-risk."
                        },
                        new {
                            name = "get_pending_stations",
                            description = "Lấy danh sách trạm sạc đang chờ Admin duyệt (trạng thái PendingApproval), gồm tên, địa chỉ, ngày gửi."
                        },
                        new {
                            name = "get_pending_disputes",
                            description = "Lấy danh sách khiếu nại (dispute) chưa được giải quyết, gồm ID, booking liên quan, lý do, mô tả, ngày tạo."
                        }
                    }
                }
            };

            return await ExecuteAiToolLoopAsync(0, request, systemPrompt, tools, "Admin");
        }

        // ============================================================
        // CORE ENGINE: Vòng lặp đệ quy Function Calling
        // Chống Infinite Loop bằng maxToolCalls (Giới hạn gọi hàm).
        // ============================================================
        private async Task<ChatbotResponseDto> ExecuteAiToolLoopAsync(
            int userId, 
            ChatbotRequestDto request, 
            string systemPrompt, 
            object tools,
            string role)
        {
            var (apiKey, model) = GetGeminiConfig();
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            // Xây dựng lịch sử trò chuyện (KHÔNG fake system prompt vào user message nữa)
            var contents = new List<object>();

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
                // Dùng system_instruction chính thức của Gemini API thay vì fake user message
                var payload = new
                {
                    system_instruction = new { parts = new[] { new { text = systemPrompt } } },
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

                // Dùng using để đảm bảo HttpResponseMessage luôn được Dispose đúng cách
                using (response)
                {
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
                            
                            // THỰC THI TOOL THEO TÊN + ROLE (Hàng rào bảo mật 100%)
                            object? resultData = await ExecuteToolAsync(role, functionName, userId);

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
                                return new ChatbotResponseDto { ReplyMarkdown = textProp.GetString() };
                            }
                        }
                    }
                } // response.Dispose() tự động ở đây
            }

            return new ChatbotResponseDto { ReplyMarkdown = "Xin lỗi, tôi phải suy nghĩ quá lâu (vượt ngưỡng an toàn) nên tự động ngắt kết nối." };
        }

        // ============================================================
        // TOOL EXECUTOR: Định tuyến và thực thi Function Calling theo Role
        // Tách riêng ra khỏi vòng lặp chính để dễ bảo trì.
        // ============================================================
        private async Task<object> ExecuteToolAsync(string role, string functionName, int userId)
        {
            try
            {
                if (role == "Driver")
                {
                    return functionName switch
                    {
                        "check_wallet_balance" => await ExecuteDriverCheckWallet(userId),
                        "find_nearby_stations" => await ExecuteFindNearbyStations(),
                        _ => new { error = "Quyền truy cập hàm bị từ chối do bảo mật." }
                    };
                }
                
                if (role == "Owner")
                {
                    return functionName switch
                    {
                        "check_owner_wallet" => await ExecuteOwnerCheckWallet(userId),
                        "get_owner_dashboard" => await ExecuteOwnerDashboard(userId),
                        "get_owner_bookings" => await ExecuteOwnerBookings(userId),
                        _ => new { error = "Quyền truy cập hàm bị từ chối do bảo mật." }
                    };
                }
                
                if (role == "Admin")
                {
                    return functionName switch
                    {
                        "get_admin_dashboard" => await ExecuteAdminDashboard(),
                        "get_pending_stations" => await ExecuteAdminPendingStations(),
                        "get_pending_disputes" => await ExecuteAdminPendingDisputes(),
                        _ => new { error = "Quyền truy cập hàm bị từ chối do bảo mật." }
                    };
                }

                return new { error = "Tool mapping not implemented for this role." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tool execution failed: {Role}/{FunctionName}", role, functionName);
                return new { error = "Đã xảy ra lỗi khi thực thi chức năng. Vui lòng thử lại." };
            }
        }

        // ============================================================
        // DRIVER TOOLS
        // ============================================================
        private async Task<object> ExecuteDriverCheckWallet(int userId)
        {
            var wallet = await _walletService.GetOrCreateWalletAsync(userId);
            return new { message = "Lấy số dư thành công", balance = wallet.AvailableBalance, frozen = wallet.FrozenBalance };
        }

        private async Task<object> ExecuteFindNearbyStations()
        {
            var stations = await _stationRepo.GetTopRatedStationsAsync(5);
            
            var result = stations.Select(s => new 
                { 
                    name = s.Name, 
                    address = s.Address, 
                    rating = s.AverageRating,
                    totalReviews = s.TotalReviews
                }).ToList();
            
            return new { message = $"Tìm thấy {result.Count} trạm sạc đang hoạt động", stations = result };
        }

        // ============================================================
        // OWNER TOOLS
        // ============================================================
        private async Task<object> ExecuteOwnerCheckWallet(int userId)
        {
            var wallet = await _walletService.GetOrCreateWalletAsync(userId);
            return new { message = "Lấy số dư ví thành công", availableBalance = wallet.AvailableBalance, frozenBalance = wallet.FrozenBalance };
        }

        private async Task<object> ExecuteOwnerDashboard(int userId)
        {
            var metrics = await _dashboardService.GetOwnerMetricsAsync(userId);
            return new 
            { 
                message = "Báo cáo kinh doanh 30 ngày qua",
                revenueLast30Days = metrics.RevenueLast30Days,
                walletBalance = metrics.WalletBalance,
                totalStations = metrics.TotalStations,
                bookingsLast30Days = metrics.BookingsLast30Days,
                cancelRate = $"{metrics.CancelRateLast30Days * 100:0.##}%",
                stationPerformances = metrics.StationPerformances.Select(s => new 
                { 
                    s.StationName, 
                    s.TotalBookings, 
                    totalRevenue = s.TotalRevenue,
                    averageRating = s.AverageRating
                }),
                topServicesSold = metrics.TopServicesSold.Select(s => new 
                { 
                    s.ServiceName, 
                    s.QuantitySold, 
                    revenue = s.Revenue
                })
            };
        }

        private async Task<object> ExecuteOwnerBookings(int userId)
        {
            var bookings = await _bookingService.GetByOwnerAsync(userId);
            var recent = bookings
                .OrderByDescending(b => b.CreatedAt)
                .Take(10)
                .Select(b => new 
                { 
                    b.Id, 
                    b.Status, 
                    b.DriverName,
                    b.StationName,
                    b.SlotName,
                    b.TotalAmount, 
                    b.StartTime, 
                    b.EndTime,
                    b.CreatedAt
                })
                .ToList();
            
            return new { message = $"Có tổng cộng {bookings.Count} đơn đặt, hiển thị 10 đơn gần nhất", bookings = recent };
        }

        // ============================================================
        // ADMIN TOOLS
        // ============================================================
        private async Task<object> ExecuteAdminDashboard()
        {
            var metrics = await _dashboardService.GetAdminMetricsAsync();
            return new 
            { 
                message = "Báo cáo tổng quan hệ thống 30 ngày qua",
                escrowBalance = metrics.TotalEscrowBalance,
                platformRevenue = metrics.TotalPlatformRevenue,
                totalStations = metrics.TotalStations,
                activeStations = metrics.TotalActiveStations,
                totalUsers = metrics.TotalUsers,
                bookingsLast30Days = metrics.BookingsLast30Days,
                cancelRate = $"{metrics.CancelRateLast30Days * 100:0.##}%",
                disputesLast30Days = metrics.DisputesLast30Days,
                topDisputedStations = metrics.TopDisputedStations.Select(s => new 
                { 
                    s.StationName, 
                    s.DisputeCount 
                }),
                highRiskDrivers = metrics.HighRiskDrivers.Select(d => new 
                { 
                    d.DriverName, 
                    d.CancelledBookings,
                    d.TotalBookings,
                    d.SuspiciousNote 
                })
            };
        }

        private async Task<object> ExecuteAdminPendingStations()
        {
            var stations = await _stationService.GetPendingStationsAsync();
            return new 
            { 
                message = $"Có {stations.Count} trạm đang chờ duyệt",
                stations = stations.Select(s => new 
                { 
                    s.Id, 
                    s.Name, 
                    s.Address,
                    s.CreatedAt
                })
            };
        }

        private async Task<object> ExecuteAdminPendingDisputes()
        {
            var disputes = await _disputeService.GetPendingAsync();
            return new 
            { 
                message = $"Có {disputes.Count} khiếu nại chưa xử lý",
                disputes = disputes.Select(d => new 
                { 
                    d.Id, 
                    d.BookingId, 
                    d.Reason,
                    d.Description,
                    d.CreatedByName,
                    d.Status, 
                    d.CreatedAt 
                })
            };
        }
    }
}
