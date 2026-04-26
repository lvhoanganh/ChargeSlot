using ChargeSlot.Api.DTOs.Payment;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IPaymentService
    {
        Task<string> CreateSePayQrUrlAsync(int bookingId, int driverUserId);
        Task<bool> ProcessSePayWebhookAsync(SePayWebhookRequest request);
    }
}
