using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class ExtraServiceConfiguration : IEntityTypeConfiguration<ExtraService>
    {
        public void Configure(EntityTypeBuilder<ExtraService> builder)
        {
            builder.ToTable("ExtraService");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.ServiceName).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Price).HasPrecision(18, 2);
            builder.HasOne(x => x.ChargingStation)
                .WithMany(s => s.ExtraServices)
                .HasForeignKey(x => x.StationId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.StationId, x.IsActive });
        }
    }
}
