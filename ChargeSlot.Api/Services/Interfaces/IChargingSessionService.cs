using ChargeSlot.Api.DTOs.Booking;
using ChargeSlot.Api.DTOs.ChargingSession;
using ChargeSlot.Api.DTOs.Invoice;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IChargingSessionService
    {
        /// <summary>Driver scans QR to check in.</summary>
        Task<ChargingSessionDto> CheckInAsync(int driverUserId, string qrCodeToken);

        /// <summary>Owner stops charging session and issues invoice.</summary>
        Task<ChargingSessionDto> StopChargingAsync(int ownerUserId, int sessionId);

        /// <summary>Driver yêu cầu kết thúc sạc sớm → Owner mới được dừng.</summary>
        Task<ChargingSessionDto> RequestEarlyEndAsync(int driverUserId, int sessionId);

        /// <summary>Driver confirms invoice and completes booking.</summary>
        Task<BookingDto> ConfirmCompletionAsync(int driverUserId, int sessionId);

        /// <summary>Get charging session by booking ID.</summary>
        Task<ChargingSessionDto?> GetByBookingIdAsync(int bookingId);

        /// <summary>Get all active (ongoing) sessions for an owner.</summary>
        Task<List<ChargingSessionDto>> GetActiveByOwnerAsync(int ownerUserId);

        /// <summary>Get invoice by booking ID.</summary>
        Task<InvoiceDto?> GetInvoiceByBookingIdAsync(int bookingId);
    }
}
