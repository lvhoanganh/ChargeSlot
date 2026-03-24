using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Station
{
    public class UpdateStationStatusDto
    {
        [Required]
        public string OperationalStatus { get; set; } = null!; // "Active" or "Inactive"
    }
}
