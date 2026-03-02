using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class StationImageConfiguration : IEntityTypeConfiguration<StationImage>
    {
        public void Configure(EntityTypeBuilder<StationImage> builder)
        {
            builder.ToTable("StationImage");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.ImageUrl).HasMaxLength(300).IsRequired();
            builder.HasOne(x => x.ChargingStation)
                .WithMany(s => s.Images)
                .HasForeignKey(x => x.StationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
