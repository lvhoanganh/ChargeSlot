using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Dispute
{
    public class OwnerEvidenceDto
    {
        [MaxLength(2000)]
        public string? Response { get; set; }

        /// <summary>Evidence files from owner.</summary>
        public List<EvidenceFileDto>? Evidences { get; set; }
    }
}
