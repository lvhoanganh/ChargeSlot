using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Admin
{
    public class UpdateSystemConfigsDto
    {
        // --------------------------------------------------------
        // SECURITY / AUTHENTICATION
        // --------------------------------------------------------
        [Required(ErrorMessage = "Bắt buộc phải nhập Mật Khẩu Cấp 2")]
        public string SecondaryPassword { get; set; } = null!;

        // --------------------------------------------------------
        // BOOKING & CANCELLATION RULES
        // --------------------------------------------------------
        [Required]
        [Range(0, 1000, ErrorMessage = "Giờ được hoàn tiền 100% phải từ 0 - 1000")]
        public int RefundPolicy100_Hrs { get; set; }

        [Required]
        [Range(0, 1000, ErrorMessage = "Giờ được hoàn tiền 50% phải từ 0 - 1000")]
        public int RefundPolicy50_Hrs { get; set; }

        [Required]
        [Range(5, 1440, ErrorMessage = "Thời hạn thanh toán booking (phút) phải từ 5 - 1440")]
        public int Payment_Expiry_Minutes { get; set; }

        [Required]
        [Range(0, 60, ErrorMessage = "Thời gian cho phép check-in sớm (phút) phải từ 0 - 60")]
        public int CheckIn_Window_Minutes { get; set; }

        [Required]
        [Range(0, 120, ErrorMessage = "Thời gian giữ slot tự động hủy (Grace period) phải từ 0 - 120 phút")]
        public int NoShow_Grace_Minutes { get; set; }

        [Required]
        [Range(0, 120, ErrorMessage = "Khoảng đệm (Buffer) giữa các slot sạc phải từ 0 - 120 phút")]
        public int Slot_Buffer_Minutes { get; set; }

        // --------------------------------------------------------
        // FINANCIAL RATIOS
        // --------------------------------------------------------
        [Required]
        [Range(0, 1.0, ErrorMessage = "Thuế VAT phải nằm trong khoảng 0.0 đến 1.0")]
        public decimal VAT_Rate { get; set; }

        [Required]
        [Range(0, 1.0, ErrorMessage = "Phí chiết khấu nền tảng phải nằm trong khoảng 0.0 đến 1.0")]
        public decimal Platform_Fee_Rate { get; set; }

        [Required]
        [Range(0, 1.0, ErrorMessage = "Tỷ lệ tích luỹ điểm thành viên phải nằm trong khoảng 0.0 đến 1.0")]
        public decimal Loyalty_Earn_Rate { get; set; }

        // --------------------------------------------------------
        // DISPUTE & REPORTING
        // --------------------------------------------------------
        [Required]
        [Range(1, 100, ErrorMessage = "Giới hạn khiếu nại (1 tháng) phải từ 1 - 100")]
        public int Dispute_Limit_Per_Month { get; set; }

        [Required]
        [Range(1, 720, ErrorMessage = "Thời gian chủ trạm kháng cáo tự động (Giờ) phải từ 1 - 720")]
        public int Dispute_OwnerEvidence_Hours { get; set; }

        [Required]
        [Range(1, 720, ErrorMessage = "Thời gian Admin review (Giờ) phải từ 1 - 720")]
        public int Dispute_AdminReview_Hours { get; set; }

        // --------------------------------------------------------
        // PUNISHMENT / BAN RULES
        // --------------------------------------------------------
        [Required]
        [Range(1, 10000, ErrorMessage = "Số ngày khóa tài khoản vĩnh viễn (Ngày) không hợp lệ")]
        public int Ban_Duration_Days_Permanent { get; set; }

        [Required]
        [Range(1, 10000, ErrorMessage = "Số ngày khóa tài khoản lần đầu (Ngày) không hợp lệ")]
        public int Ban_Duration_Days_FirstOffense { get; set; }

        // --------------------------------------------------------
        // OTP & COMMUNICATION
        // --------------------------------------------------------
        [Required]
        [Range(1, 60, ErrorMessage = "Thời hạn sống của OTP (Phút) phải từ 1 - 60")]
        public int OTP_Expiry_Minutes { get; set; }

        [Required]
        [Range(10, 300, ErrorMessage = "Thời gian chờ giữa 2 lần gửi OTP (Giây) phải từ 10 - 300")]
        public int OTP_Cooldown_Seconds { get; set; }

        // --------------------------------------------------------
        // AUTO-CONFIRM DEADLINES
        // --------------------------------------------------------
        [Required]
        [Range(1, 168, ErrorMessage = "Giờ auto-confirm rút tiền phải từ 1 - 168")]
        public int Withdraw_AutoConfirm_Hours { get; set; }

        [Required]
        [Range(1, 168, ErrorMessage = "Giờ auto-confirm hóa đơn phải từ 1 - 168")]
        public int Invoice_AutoConfirm_Hours { get; set; }

        [Required]
        [Range(1, 24, ErrorMessage = "Cửa sổ nhắc nhở sắp hết hạn (Giờ) phải từ 1 - 24")]
        public int Reminder_Window_Hours { get; set; }
    }
}
