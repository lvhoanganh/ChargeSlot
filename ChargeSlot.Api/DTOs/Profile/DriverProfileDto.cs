using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Profile
{
    public class DriverProfileDto
    {
        [MaxLength(50)]
        public string? VehicleType { get; set; }

        [MaxLength(20)]
        public string? LicensePlate { get; set; }

        [MaxLength(50)]
        public string? LicenseNumber { get; set; }
    }
}

