namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IPaymentService
    {
        Task<string> CreatePaymentUrlAsync(int bookingId, int driverUserId, HttpContext context);
        Task<bool> ProcessVnPayCallbackAsync(IQueryCollection query);
    }
}
