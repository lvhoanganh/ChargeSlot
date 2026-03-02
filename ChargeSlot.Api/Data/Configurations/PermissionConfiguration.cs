using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
    {
        public void Configure(EntityTypeBuilder<Permission> builder)
        {
            builder.ToTable("Permission");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
            builder.HasIndex(x => x.Code).IsUnique();
            builder.Property(x => x.Description).HasMaxLength(255);
        }
    }
}
