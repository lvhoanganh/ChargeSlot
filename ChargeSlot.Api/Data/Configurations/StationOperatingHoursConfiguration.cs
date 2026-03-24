using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class StationOperatingHoursConfiguration : IEntityTypeConfiguration<StationOperatingHours>
    {
        public void Configure(EntityTypeBuilder<StationOperatingHours> builder)
        {
            builder.ToTable("StationOperatingHours");
            builder.HasKey(x => new { x.StationId, x.DayOfWeek });
            builder.HasOne(x => x.ChargingStation)
                .WithMany(s => s.OperatingHours)
                .HasForeignKey(x => x.StationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
