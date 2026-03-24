using ChargeSlot.Api.Services.Interfaces;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Services.Implementation
{
    public class VnPayService : IVnPayService
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<VnPayService> _logger;

        public VnPayService(IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<VnPayService> logger)
        {
            _config = config;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Tạo URL thanh toán VNPay (Step 17: Create payment request)
        /// </summary>
        public string CreatePaymentUrl(int bookingId, decimal amount, string orderInfo, HttpContext context)
        {
            var vnpay = _config.GetSection("VnPay");
            var tmnCode = vnpay["TmnCode"]!;
            var hashSecret = vnpay["HashSecret"]!;
            var paymentUrl = vnpay["PaymentUrl"]!;
            var returnUrl = vnpay["ReturnUrl"]!;
            var version = vnpay["Version"] ?? "2.1.0";
            var command = vnpay["Command"] ?? "pay";
            var currCode = vnpay["CurrCode"] ?? "VND";
            var locale = vnpay["Locale"] ?? "vn";
            var orderType = vnpay["OrderType"] ?? "other";

            var txnRef = bookingId.ToString() + "_" + DateTimeHelper.VietnamNow().Ticks;
            var createDate = DateTimeHelper.VietnamNow().ToString("yyyyMMddHHmmss");
            var expireDate = DateTimeHelper.VietnamNow().AddMinutes(15).ToString("yyyyMMddHHmmss");
            var ipAddress = GetIpAddress(context);

            // VNPay amount = amount * 100 (không có phần thập phân)
            var vnpAmount = ((long)(amount * 100)).ToString();

            var vnpParams = new SortedDictionary<string, string>
            {
                { "vnp_Version", version },
                { "vnp_Command", command },
                { "vnp_TmnCode", tmnCode },
                { "vnp_Amount", vnpAmount },
                { "vnp_CurrCode", currCode },
                { "vnp_TxnRef", txnRef },
                { "vnp_OrderInfo", orderInfo },
                { "vnp_OrderType", orderType },
                { "vnp_Locale", locale },
                { "vnp_ReturnUrl", returnUrl },
                { "vnp_IpAddr", ipAddress },
                { "vnp_CreateDate", createDate },
                { "vnp_ExpireDate", expireDate }
            };

            var queryString = BuildQueryString(vnpParams);
            var signData = queryString;
            var secureHash = HmacSha512(hashSecret, signData);

            return $"{paymentUrl}?{queryString}&vnp_SecureHash={secureHash}";
        }

        /// <summary>
        /// Validate VNPay callback (Step 22-25: Process payment → Confirmed?)
        /// </summary>
        public (bool isValid, string responseCode, string txnRef) ValidateCallback(IQueryCollection query)
        {
            var vnpay = _config.GetSection("VnPay");
            var hashSecret = vnpay["HashSecret"]!;

            var vnpParams = new SortedDictionary<string, string>();
            string vnpSecureHash = "";

            foreach (var (key, value) in query)
            {
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                {
                    if (key == "vnp_SecureHash" || key == "vnp_SecureHashType")
                    {
                        if (key == "vnp_SecureHash")
                            vnpSecureHash = value.ToString();
                        continue;
                    }
                    vnpParams[key] = value.ToString();
                }
            }

            var signData = BuildQueryString(vnpParams);
            var checkSignature = HmacSha512(hashSecret, signData);

            var isValid = checkSignature.Equals(vnpSecureHash, StringComparison.InvariantCultureIgnoreCase);
            var responseCode = vnpParams.GetValueOrDefault("vnp_ResponseCode", "99");
            var txnRef = vnpParams.GetValueOrDefault("vnp_TxnRef", "");

            return (isValid, responseCode, txnRef);
        }

        /// <summary>
        /// Gọi VNPay QueryDR API để kiểm tra trạng thái giao dịch thực tế.
        /// Dùng khi PaymentExpiryJob cần xác nhận trước khi hủy booking.
        /// Doc: https://sandbox.vnpayment.vn/apis/docs/truy-van-giao-dich/
        /// </summary>
        public async Task<(bool isPaid, string responseCode)> QueryTransactionAsync(
            string txnRef, decimal amount, DateTime createdAt)
        {
            try
            {
                var vnpay = _config.GetSection("VnPay");
                var tmnCode = vnpay["TmnCode"]!;
                var hashSecret = vnpay["HashSecret"]!;
                var queryApiUrl = vnpay["QueryApiUrl"]
                    ?? "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";

                var requestId = Guid.NewGuid().ToString("N");
                var vnpAmount = ((long)(amount * 100)).ToString();
                var createDate = createdAt.ToString("yyyyMMddHHmmss");
                var transDate = createdAt.ToString("yyyyMMddHHmmss");
                var ipAddr = "127.0.0.1";
                var orderInfo = $"Query transaction {txnRef}";

                // Build data string theo thứ tự VNPay quy định
                var signData = $"{requestId}|{vnpay["Version"] ?? "2.1.0"}|querydr|{tmnCode}|{txnRef}|{transDate}|{createDate}|{ipAddr}|{orderInfo}";
                var secureHash = HmacSha512(hashSecret, signData);

                var requestBody = new
                {
                    vnp_RequestId = requestId,
                    vnp_Version = vnpay["Version"] ?? "2.1.0",
                    vnp_Command = "querydr",
                    vnp_TmnCode = tmnCode,
                    vnp_TxnRef = txnRef,
                    vnp_OrderInfo = orderInfo,
                    vnp_TransactionDate = transDate,
                    vnp_CreateDate = createDate,
                    vnp_IpAddr = ipAddr,
                    vnp_SecureHash = secureHash
                };

                var client = _httpClientFactory.CreateClient();
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(queryApiUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("VNPay QueryDR response for {TxnRef}: {Response}", txnRef, responseBody);

                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                var vnpResponseCode = root.TryGetProperty("vnp_ResponseCode", out var rc)
                    ? rc.GetString() ?? "99"
                    : "99";

                var vnpTransactionStatus = root.TryGetProperty("vnp_TransactionStatus", out var ts)
                    ? ts.GetString() ?? "99"
                    : "99";

                // vnp_ResponseCode = "00" → query thành công
                // vnp_TransactionStatus = "00" → giao dịch đã thanh toán thành công
                var isPaid = vnpResponseCode == "00" && vnpTransactionStatus == "00";

                return (isPaid, vnpTransactionStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VNPay QueryDR failed for txnRef {TxnRef}", txnRef);
                // Lỗi query → trả về false, KHÔNG tự tin expire
                // Caller nên xử lý: skip expire, thử lại lần sau
                return (false, "QUERY_ERROR");
            }
        }

        private static string BuildQueryString(SortedDictionary<string, string> data)
        {
            var sb = new StringBuilder();
            foreach (var (key, value) in data)
            {
                if (sb.Length > 0) sb.Append('&');
                sb.Append(WebUtility.UrlEncode(key));
                sb.Append('=');
                sb.Append(WebUtility.UrlEncode(value));
            }
            return sb.ToString();
        }

        private static string HmacSha512(string key, string data)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            using var hmac = new HMACSHA512(keyBytes);
            var hashBytes = hmac.ComputeHash(dataBytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        private static string GetIpAddress(HttpContext context)
        {
            var ip = context.Connection.RemoteIpAddress?.ToString();
            if (ip == "::1") ip = "127.0.0.1";
            return ip ?? "127.0.0.1";
        }
    }
}
