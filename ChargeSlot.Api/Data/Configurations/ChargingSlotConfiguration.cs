using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class ChargingSlotConfiguration : IEntityTypeConfiguration<ChargingSlot>
    {
        public void Configure(EntityTypeBuilder<ChargingSlot> builder)
        {
            builder.ToTable("ChargingSlot");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.SlotName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.PositionX).HasPrecision(10, 2);
            builder.Property(x => x.PositionY).HasPrecision(10, 2);
            builder.Property(x => x.QrCodeToken).HasMaxLength(50);
            builder.HasIndex(x => x.QrCodeToken).IsUnique().HasFilter("[QrCodeToken] IS NOT NULL");
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);

            builder.HasOne(x => x.ChargingStation)
                .WithMany(s => s.ChargingSlots)
                .HasForeignKey(x => x.StationId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.StationId, x.Status });
        }
    }
}
