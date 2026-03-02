using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class DisputeEvidenceConfiguration : IEntityTypeConfiguration<DisputeEvidence>
    {
        public void Configure(EntityTypeBuilder<DisputeEvidence> builder)
        {
            builder.ToTable("DisputeEvidence");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.FileUrl).HasMaxLength(500).IsRequired();
            builder.Property(x => x.FileType).HasMaxLength(20).IsRequired();
            builder.HasOne(x => x.Dispute)
                .WithMany(d => d.Evidences)
                .HasForeignKey(x => x.DisputeId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
