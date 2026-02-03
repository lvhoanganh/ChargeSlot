using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class ChargingSlotConfiguration : IEntityTypeConfiguration<ChargingSlot>
    {
        public void Configure(EntityTypeBuilder<ChargingSlot> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.SlotName)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(x => x.PricePerHour)
                   .HasPrecision(18, 2);

            builder.HasOne(x => x.ChargingStation)
                   .WithMany(s => s.ChargingSlots)
                   .HasForeignKey(x => x.ChargingStationId);
        }
    }
}
