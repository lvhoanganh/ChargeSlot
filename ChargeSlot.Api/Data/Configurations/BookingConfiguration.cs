using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class BookingConfiguration : IEntityTypeConfiguration<Booking>
    {
        public void Configure(EntityTypeBuilder<Booking> builder)
        {
            builder.ToTable("Booking");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.DurationHours).HasPrecision(6, 2);
            builder.Property(x => x.Note).HasMaxLength(1000);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            builder.Property(x => x.CancelReason).HasMaxLength(500);
            builder.Property(x => x.RejectionReason).HasMaxLength(500);
            builder.Property(x => x.TotalAmount).HasPrecision(18, 2);

            builder.HasOne(x => x.Driver)
                .WithMany(d => d.Bookings)
                .HasForeignKey(x => x.DriverUserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.ChargingSlot)
                .WithMany(s => s.Bookings)
                .HasForeignKey(x => x.SlotId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.SlotId, x.StartTime, x.EndTime });
            builder.HasIndex(x => new { x.DriverUserId, x.CreatedAt });
            builder.HasIndex(x => new { x.Status, x.StartTime });
        }
    }
}
