using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
    {
        public void Configure(EntityTypeBuilder<Invoice> builder)
        {
            builder.ToTable("Invoice");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.ChargingAmount).HasPrecision(18, 2);
            builder.Property(x => x.ServiceAmount).HasPrecision(18, 2);
            builder.Property(x => x.VatAmount).HasPrecision(18, 2);
            builder.Property(x => x.PlatformFee).HasPrecision(18, 2);
            builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            builder.HasOne(x => x.Booking)
                .WithOne(b => b.Invoice)
                .HasForeignKey<Invoice>(x => x.BookingId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.Status, x.CreatedAt });
        }
    }
}
