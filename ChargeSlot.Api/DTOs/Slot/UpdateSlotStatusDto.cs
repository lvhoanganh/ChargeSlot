using System.ComponentModel.DataAnnotations;
using ChargeSlot.Api.Enums;

namespace ChargeSlot.Api.DTOs.Slot
{
    public class UpdateSlotStatusDto
    {
        /// <summary>Allowed values: Active, Inactive, Maintenance.</summary>
        [Required]
        public SlotStatus Status { get; set; }
    }
}
