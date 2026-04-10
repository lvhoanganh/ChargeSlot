using System;
using System.Collections.Generic;

namespace ChargeSlot.Api.DTOs.Admin
{
    public class AdminDriverDetailDto
    {
        public int UserId { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public DateTime CreatedAt { get; set; }

        public string? VehicleType { get; set; }
        public string? LicensePlate { get; set; }
        public string? LicenseNumber { get; set; }
        public decimal LoyaltyPoints { get; set; }

        public AdminDriverWalletDto Wallet { get; set; } = null!;

        public List<AdminDriverRecentBookingDto> RecentBookings { get; set; } = new();
    }

    public class AdminDriverWalletDto
    {
        public int WalletId { get; set; }
        public decimal AvailableBalance { get; set; }
        public decimal FrozenBalance { get; set; }
    }

    public class AdminDriverRecentBookingDto
    {
        public int BookingId { get; set; }
        public string StationName { get; set; } = string.Empty;
        public string SlotName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
