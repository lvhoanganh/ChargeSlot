namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IVnPayService
    {
        string CreatePaymentUrl(int bookingId, decimal amount, string orderInfo, HttpContext context);
        (bool isValid, string responseCode, string txnRef) ValidateCallback(IQueryCollection query);
    }
}
