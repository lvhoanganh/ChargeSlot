namespace ChargeSlot.Api.DTOs.Admin
{
    /// <summary>
    /// Config công khai cho frontend/app hiển thị — KHÔNG chứa thông tin nhạy cảm.
    /// </summary>
    public class PublicSystemConfigDto
    {
        /// <summary>Đặt trước tối thiểu bao nhiêu phút</summary>
        public int Min_Booking_Lead_Minutes { get; set; }

        /// <summary>Buffer giữa 2 booking liên tiếp (phút)</summary>
        public int Slot_Buffer_Minutes { get; set; }

        /// <summary>Thời gian thanh toán sau khi Owner accept (phút)</summary>
        public int Payment_Expiry_Minutes { get; set; }

        /// <summary>Check-in sớm được bao nhiêu phút</summary>
        public int CheckIn_Window_Minutes { get; set; }

        /// <summary>Hoàn 100% nếu hủy trước N giờ</summary>
        public int RefundPolicy100_Hrs { get; set; }

        /// <summary>Hoàn 50% nếu hủy trước N giờ</summary>
        public int RefundPolicy50_Hrs { get; set; }
    }
}
