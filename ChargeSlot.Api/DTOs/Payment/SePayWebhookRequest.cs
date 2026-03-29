namespace ChargeSlot.Api.DTOs.Payment
{
    public class SePayWebhookRequest
    {
        public int id { get; set; }
        public string? gateway { get; set; }
        public string? transactionDate { get; set; }
        public string? accountNumber { get; set; }
        public string? subAccount { get; set; }
        public decimal amountIn { get; set; }
        public decimal amountOut { get; set; }
        public decimal accumulated { get; set; }
        public string? code { get; set; }
        public string? transactionContent { get; set; }
        public string? referenceCode { get; set; }
        public string? body { get; set; }
    }

    public class SePayWebhookResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
    }
}
