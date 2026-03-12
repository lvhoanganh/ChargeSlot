using ChargeSlot.Api.Services.Interfaces;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace ChargeSlot.Api.Services.Implementation
{
    public class VnPayService : IVnPayService
    {
        private readonly IConfiguration _config;

        public VnPayService(IConfiguration config)
        {
            _config = config;
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

            var txnRef = bookingId.ToString() + "_" + DateTime.UtcNow.Ticks;
            var createDate = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss");
            var expireDate = DateTime.UtcNow.AddHours(7).AddMinutes(15).ToString("yyyyMMddHHmmss");
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
