using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Station
{
    public class ReviewStationDto
    {
        [Required]
        public bool IsApproved { get; set; }

        [MaxLength(2000)]
        public string? AdminNote { get; set; }
    }
}
