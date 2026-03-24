using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models.Identity;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 Dispute - driver submits, admin resolves (BR-70–74).</summary>
    public class Dispute
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public Booking Booking { get; set; } = null!;

        public int? InvoiceId { get; set; }
        public Invoice? Invoice { get; set; }

        public int CreatedByUserId { get; set; }
        public ApplicationUser CreatedByUser { get; set; } = null!;

        public string Reason { get; set; } = null!;
        public string Description { get; set; } = null!;
        public DisputeStatus Status { get; set; } = DisputeStatus.Open;
        public string? OwnerResponse { get; set; }
        public string? AdminNote { get; set; }
        /// <summary>Admin Id (0) — không FK vì Admin config trong appsettings, không lưu DB.</summary>
        public int? ResolvedByUserId { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTimeHelper.VietnamNow();

        public ICollection<DisputeEvidence> Evidences { get; set; } = new List<DisputeEvidence>();
    }
}
