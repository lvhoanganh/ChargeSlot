using System;
using System.Collections.Generic;

namespace ChargeSlot.Api.DTOs.Admin
{
    public class AdminOwnerDetailDto
    {
        public int UserId { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public DateTime CreatedAt { get; set; }

        public AdminOwnerKycDto Kyc { get; set; } = null!;
        public AdminOwnerWalletDto Wallet { get; set; } = null!;

        public List<AdminOwnerStationDto> Stations { get; set; } = new();
    }

    public class AdminOwnerKycDto
    {
        public string BusinessName { get; set; } = string.Empty;
        public string TaxCode { get; set; } = string.Empty;
        public string? IdCardNumber { get; set; }
        public string? IdCardDate { get; set; }
        public string? FrontIdCardUrl { get; set; }
        public string? BackIdCardUrl { get; set; }
        public string? BusinessLicenseNumber { get; set; }
        public string? BusinessLicenseUrl { get; set; }
        public string? Address { get; set; }
        public string KycStatus { get; set; } = string.Empty;
        public string? KycRejectReason { get; set; }
    }

    public class AdminOwnerWalletDto
    {
        public int WalletId { get; set; }
        public decimal AvailableBalance { get; set; }
        public decimal FrozenBalance { get; set; }
    }

    public class AdminOwnerStationDto
    {
        public int StationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string ApprovalStatus { get; set; } = string.Empty;
        public string OperationalStatus { get; set; } = string.Empty;
        public decimal AverageRating { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
