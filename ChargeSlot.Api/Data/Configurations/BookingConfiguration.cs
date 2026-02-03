using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class BookingConfiguration : IEntityTypeConfiguration<Booking>
    {
        public void Configure(EntityTypeBuilder<Booking> builder)
        {
            builder.HasKey(x => x.Id);

            // Booking -> Driver (Identity User)
            builder.HasOne(x => x.Driver)
                   .WithMany()
                   .HasForeignKey(x => x.DriverId)
                   .OnDelete(DeleteBehavior.Restrict);


            // Booking -> ChargingSlot
            builder.HasOne(x => x.ChargingSlot)
                   .WithMany(s => s.Bookings)
                   .HasForeignKey(x => x.ChargingSlotId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
