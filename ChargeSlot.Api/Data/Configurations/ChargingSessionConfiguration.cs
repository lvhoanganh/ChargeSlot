using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class ChargingSessionConfiguration : IEntityTypeConfiguration<ChargingSession>
    {
        public void Configure(EntityTypeBuilder<ChargingSession> builder)
        {
            builder.ToTable("ChargingSession");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.ActualDurationHours).HasPrecision(8, 2);
            builder.HasOne(x => x.Booking)
                .WithOne(b => b.ChargingSession)
                .HasForeignKey<ChargingSession>(x => x.BookingId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
