using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class ChargingStationConfiguration : IEntityTypeConfiguration<ChargingStation>
    {
        public void Configure(EntityTypeBuilder<ChargingStation> builder)
        {
            builder.ToTable("ChargingStation");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(255).IsRequired();
            builder.Property(x => x.Address).HasMaxLength(300).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(2000);
            builder.Property(x => x.Latitude).HasPrecision(9, 6);
            builder.Property(x => x.Longitude).HasPrecision(9, 6);

            // Sơ đồ mặt bằng trạm sạc
            builder.Property(x => x.LayoutImageUrl).HasMaxLength(500);
            builder.Property(x => x.LayoutWidth).HasPrecision(10, 2);
            builder.Property(x => x.LayoutHeight).HasPrecision(10, 2);

            builder.Property(x => x.ApprovalStatus).HasConversion<string>().HasMaxLength(30);
            builder.Property(x => x.OperationalStatus).HasConversion<string>().HasMaxLength(30);
            builder.Property(x => x.AdminNote).HasMaxLength(2000);

            builder.HasOne(x => x.Owner)
                .WithMany(o => o.ChargingStations)
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ReviewedByUserId: Admin Id=0, không FK vì Admin không lưu DB
            builder.Property(x => x.ReviewedByUserId);

            builder.HasIndex(x => x.OwnerUserId);
            builder.HasIndex(x => new { x.ApprovalStatus, x.OperationalStatus });
        }
    }
}
