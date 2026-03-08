using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class SlotPricingConfiguration : IEntityTypeConfiguration<SlotPricing>
    {
        public void Configure(EntityTypeBuilder<SlotPricing> builder)
        {
            builder.ToTable("SlotPricing");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.PricePerHour).HasPrecision(18, 2);
            builder.Property(x => x.Priority).HasDefaultValue(0);
            builder.HasOne(x => x.ChargingSlot)
                .WithMany(s => s.SlotPricings)
                .HasForeignKey(x => x.SlotId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(x => new { x.SlotId, x.IsActive });
        }
    }
}
