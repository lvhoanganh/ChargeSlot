using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.ToTable("Payment");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Amount).HasPrecision(18, 2);
            builder.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(30);
            builder.Property(x => x.GatewayTxnRef).HasMaxLength(100);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            builder.HasOne(x => x.Booking)
                .WithOne(b => b.Payment)
                .HasForeignKey<Payment>(x => x.BookingId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.BookingId, x.CreatedAt });
        }
    }
}
