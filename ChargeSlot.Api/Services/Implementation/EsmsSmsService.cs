using ChargeSlot.Api.Services.Interfaces;
using System.Text;
using System.Text.Json;

namespace ChargeSlot.Api.Services.Implementation
{
    public class EsmsSmsService : ISmsService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _secretKey;
        private readonly string _smsType;
        private readonly string? _brandName;
        private readonly ILogger<EsmsSmsService> _logger;

        public EsmsSmsService(IConfiguration config, HttpClient http, ILogger<EsmsSmsService> logger)
        {
            _http = http;
            _logger = logger;
            var section = config.GetSection("Esms");
            _apiKey = section["ApiKey"] ?? throw new InvalidOperationException("Esms:ApiKey missing");
            _secretKey = section["SecretKey"] ?? throw new InvalidOperationException("Esms:SecretKey missing");
            _smsType = section["SmsType"] ?? "2";
            _brandName = section["BrandName"];
        }

        public async Task SendSmsAsync(string phoneNumber, string content)
        {
            var url = "http://rest.esms.vn/MainService.svc/json/SendMultipleMessage_V4_post_json/";

            var body = new Dictionary<string, object>
            {
                ["ApiKey"] = _apiKey,
                ["Content"] = content,
                ["Phone"] = phoneNumber,
                ["SecretKey"] = _secretKey,
                ["SmsType"] = _smsType,
                ["IsUnicode"] = "0"
            };

            if (!string.IsNullOrEmpty(_brandName))
                body["Brandname"] = _brandName;

            var json = JsonSerializer.Serialize(body);
            var request = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _http.PostAsync(url, request);
                var result = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("[eSMS] Phone: {Phone} | Response: {Result}", phoneNumber, result);

                // CodeResult == "100" là thành công
                using var doc = JsonDocument.Parse(result);
                var code = doc.RootElement.GetProperty("CodeResult").GetString();
                if (code != "100")
                {
                    var errMsg = doc.RootElement.GetProperty("ErrorMessage").GetString();
                    _logger.LogWarning("[eSMS] Failed: {Code} - {Error}", code, errMsg);
                    throw new InvalidOperationException($"Gửi SMS thất bại: {errMsg}");
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[eSMS] HTTP error sending SMS to {Phone}", phoneNumber);
                throw new InvalidOperationException("Không thể gửi SMS. Vui lòng thử lại sau.");
            }
        }
    }
}
