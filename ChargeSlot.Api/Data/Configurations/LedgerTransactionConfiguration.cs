using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class LedgerTransactionConfiguration : IEntityTypeConfiguration<LedgerTransaction>
    {
        public void Configure(EntityTypeBuilder<LedgerTransaction> builder)
        {
            builder.ToTable("LedgerTransaction");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.ReferenceType).HasMaxLength(30).IsRequired();
            builder.Property(x => x.Memo).HasMaxLength(500);
            builder.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
