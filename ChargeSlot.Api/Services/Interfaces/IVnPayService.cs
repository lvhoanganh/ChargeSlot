namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IVnPayService
    {
        string CreatePaymentUrl(int bookingId, decimal amount, string orderInfo, HttpContext context);
        (bool isValid, string responseCode, string txnRef) ValidateCallback(IQueryCollection query);

        /// <summary>
        /// Gọi VNPay QueryDR API để kiểm tra trạng thái giao dịch thực tế.
        /// Trả về (isPaid, responseCode). isPaid = true nếu giao dịch đã thành công.
        /// </summary>
        Task<(bool isPaid, string responseCode)> QueryTransactionAsync(string txnRef, decimal amount, DateTime createdAt);
    }
}
