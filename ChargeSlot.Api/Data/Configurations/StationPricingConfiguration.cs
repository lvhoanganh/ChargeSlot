using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class StationPricingConfiguration : IEntityTypeConfiguration<StationPricing>
    {
        public void Configure(EntityTypeBuilder<StationPricing> builder)
        {
            builder.ToTable("StationPricing");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.PricePerHour).HasPrecision(18, 2);
            builder.Property(x => x.Priority).HasDefaultValue(0);
            builder.HasOne(x => x.ChargingStation)
                .WithMany(s => s.StationPricings)
                .HasForeignKey(x => x.StationId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(x => new { x.StationId, x.IsActive });
        }
    }
}
