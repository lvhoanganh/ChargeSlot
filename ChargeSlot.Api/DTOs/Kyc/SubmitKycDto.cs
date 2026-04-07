using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Kyc
{
    public class SubmitKycDto
    {
        [Required(ErrorMessage = "Số chứng minh / CCCD là bắt buộc")]
        public string IdCardNumber { get; set; } = null!;

        [Required(ErrorMessage = "Ngày cấp CCCD là bắt buộc")]
        public string IdCardDate { get; set; } = null!;

        [Required(ErrorMessage = "Tên doanh nghiệp / hộ kinh doanh là bắt buộc")]
        public string BusinessName { get; set; } = null!;

        [Required(ErrorMessage = "Mã số Giấy phép kinh doanh (GPKD) là bắt buộc")]
        public string BusinessLicenseNumber { get; set; } = null!;

        [Required(ErrorMessage = "Mã số thuế là bắt buộc")]
        public string TaxCode { get; set; } = null!;

        [Required(ErrorMessage = "Địa chỉ trụ sở là bắt buộc")]
        public string Address { get; set; } = null!;

        [Required(ErrorMessage = "Ảnh mặt trước CCCD là bắt buộc")]
        public IFormFile FrontIdCardImage { get; set; } = null!;

        [Required(ErrorMessage = "Ảnh mặt sau CCCD là bắt buộc")]
        public IFormFile BackIdCardImage { get; set; } = null!;

        [Required(ErrorMessage = "Ảnh giấy phép kinh doanh là bắt buộc")]
        public IFormFile BusinessLicenseImage { get; set; } = null!;
    }
}
