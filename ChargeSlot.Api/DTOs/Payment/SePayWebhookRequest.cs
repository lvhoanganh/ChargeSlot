namespace ChargeSlot.Api.DTOs.Payment
{
    /// <summary>
    /// DTO nhận Webhook từ SePay — đúng 100% theo docs.sepay.vn/tich-hop-webhooks.html
    /// </summary>
    public class SePayWebhookRequest
    {
        public int id { get; set; }                    // ID giao dịch trên SePay (dùng chống trùng lặp)
        public string? gateway { get; set; }           // Brand name ngân hàng (VD: "MBBank")
        public string? transactionDate { get; set; }   // Thời gian giao dịch ("2023-03-25 14:02:37")
        public string? accountNumber { get; set; }     // Số tài khoản ngân hàng
        public string? code { get; set; }              // Mã code thanh toán (SePay tự nhận diện)
        public string? content { get; set; }           // Nội dung chuyển khoản ★
        public string? transferType { get; set; }      // "in" = tiền vào, "out" = tiền ra ★
        public decimal transferAmount { get; set; }    // Số tiền giao dịch ★
        public decimal accumulated { get; set; }       // Số dư tài khoản (lũy kế)
        public string? subAccount { get; set; }        // Tài khoản phụ (VA)
        public string? referenceCode { get; set; }     // Mã tham chiếu SMS
        public string? description { get; set; }       // Toàn bộ nội dung tin nhắn SMS
    }

    public class SePayWebhookResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
    }
}
