using ChargeSlot.Api.DTOs.Booking;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IBookingService
    {
        Task<BookingDto> CreateBookingAsync(int driverUserId, CreateBookingDto dto);
        Task<BookingDto> AcceptBookingAsync(int ownerUserId, int bookingId);
        Task<BookingDto> RejectBookingAsync(int ownerUserId, int bookingId, RejectBookingDto dto);
        Task<BookingDto> DriverCancelBookingAsync(int driverUserId, int bookingId, string? cancelReason);
        Task<BookingDto> OwnerCancelBookingAsync(int ownerUserId, int bookingId, string? cancelReason);
        Task<BookingDto?> GetByIdAsync(int bookingId);
        Task<CancelPreviewDto> GetCancelPreviewAsync(int driverUserId, int bookingId);
        Task<List<BookingDto>> GetByDriverAsync(int driverUserId);
        Task<List<BookingDto>> GetByOwnerAsync(int ownerUserId);
    }
}
