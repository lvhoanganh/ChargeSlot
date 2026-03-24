using ChargeSlot.Api.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
    {
        public void Configure(EntityTypeBuilder<ApplicationUser> builder)
        {
            builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            builder.Property(u => u.PhoneNumber).HasMaxLength(20).IsRequired();
            builder.Property(u => u.AvatarUrl).HasMaxLength(300);
            builder.Property(u => u.Status).HasMaxLength(30);
            builder.HasIndex(u => u.PhoneNumber).IsUnique();
        }
    }
}
