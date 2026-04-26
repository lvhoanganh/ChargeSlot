using ChargeSlot.Api.DTOs.Booking;
using ChargeSlot.Api.DTOs;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IBookingService
    {
        Task<BookingDto> CreateBookingAsync(int driverUserId, CreateBookingDto dto);
        Task<BookingDto> AcceptBookingAsync(int ownerUserId, int bookingId);
        Task<BookingDto> RejectBookingAsync(int ownerUserId, int bookingId, RejectBookingDto dto);
        Task<BookingDto> DriverCancelBookingAsync(int driverUserId, int bookingId, string? cancelReason);
        Task<BookingDto> OwnerCancelBookingAsync(int ownerUserId, int bookingId, string? cancelReason);
        Task<BookingDetailDto?> GetByIdAsync(int bookingId);
        Task<CancelPreviewDto> GetCancelPreviewAsync(int driverUserId, int bookingId);
        Task<PagedResultDto<BookingDto>> GetByDriverPagedAsync(int driverUserId, string? status, DateTime? fromDate, DateTime? toDate, int page, int pageSize);
        Task<PagedResultDto<BookingDto>> GetDriverHistoryPagedAsync(int driverUserId, DateTime? fromDate, DateTime? toDate, int page, int pageSize);
        Task<PagedResultDto<BookingDto>> GetByOwnerPagedAsync(int ownerUserId, string? status, DateTime? fromDate, DateTime? toDate, int page, int pageSize);
        
        /// <summary>
        /// Dùng cho hệ thống gỡ/huỷ Booking khi tài khoản bị khóa (Admin).
        /// Trả Stock, nhả Slot, hoàn tiền 100% nếu đã Paid.
        /// </summary>
        Task CancelSystemBookingAsync(int bookingId, string systemReason);
        
        /// <summary>
        /// Dùng cho Background Jobs khi Booking bị quá hạn (hết giờ Owner duyệt, hết giờ thanh toán).
        /// Cập nhật trạng thái Expired, hoàn lại Điểm, hoàn Tồn kho, và nhả Slot.
        /// </summary>
        Task ExpireSystemBookingAsync(int bookingId, string reason);

        Task<ChargeSlot.Api.DTOs.PagedResultDto<BookingDto>> GetAdminAllBookingsAsync(ChargeSlot.Api.DTOs.Admin.Overview.BookingFilterDto filter);
    }
}
