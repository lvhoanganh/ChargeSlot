using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.ChargingSession
{
    public class CheckInDto
    {
        [Required]
        public string QrCodeToken { get; set; } = null!;
    }
}
