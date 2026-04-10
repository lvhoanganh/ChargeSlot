using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class SystemConfigConfiguration : IEntityTypeConfiguration<SystemConfig>
    {
        public void Configure(EntityTypeBuilder<SystemConfig> builder)
        {
            builder.HasKey(c => c.Key);
            builder.Property(c => c.Key).HasMaxLength(100);
            builder.Property(c => c.Value).HasMaxLength(500).IsRequired();
            builder.Property(c => c.Description).HasMaxLength(500);
        }
    }
}
