using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class PayoutRequestConfiguration : IEntityTypeConfiguration<PayoutRequest>
    {
        public void Configure(EntityTypeBuilder<PayoutRequest> builder)
        {
            builder.ToTable("PayoutRequest");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Amount).HasPrecision(18, 2);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            builder.Property(x => x.Note).HasMaxLength(2000);

            builder.HasOne(x => x.Owner)
                .WithMany(o => o.PayoutRequests)
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.BankAccount)
                .WithMany(b => b.PayoutRequests)
                .HasForeignKey(x => x.BankAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.ProcessedByUser)
                .WithMany()
                .HasForeignKey(x => x.ProcessedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.OwnerUserId, x.RequestedAt });
        }
    }
}
