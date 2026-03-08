using ChargeSlot.Api.Models;
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

            // ProcessedByUserId: Admin Id=0, không FK vì Admin không lưu DB
            builder.Property(x => x.ProcessedByUserId);

            builder.HasIndex(x => new { x.OwnerUserId, x.RequestedAt });
        }
    }
}
