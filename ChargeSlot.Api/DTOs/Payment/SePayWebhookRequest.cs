namespace ChargeSlot.Api.DTOs.Payment
{
    public class SePayWebhookRequest
    {
        public string id { get; set; } = string.Empty;
        public string gateway { get; set; } = string.Empty;
        public string transactionDate { get; set; } = string.Empty;
        public string accountNumber { get; set; } = string.Empty;
        public string subAccount { get; set; } = string.Empty;
        public decimal amountIn { get; set; }
        public decimal amountOut { get; set; }
        public decimal accumulated { get; set; }
        public string code { get; set; } = string.Empty;
        public string transactionContent { get; set; } = string.Empty;
        public string referenceCode { get; set; } = string.Empty;
        public string body { get; set; } = string.Empty;
    }

    public class SePayWebhookResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
    }
}
