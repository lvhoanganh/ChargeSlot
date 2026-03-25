using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class DriverConfiguration : IEntityTypeConfiguration<Driver>
    {
        public void Configure(EntityTypeBuilder<Driver> builder)
        {
            builder.ToTable("Driver");
            builder.HasKey(x => x.UserId);
            builder.HasOne(x => x.User)
                .WithOne(u => u.DriverProfile)
                .HasForeignKey<Driver>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(x => x.LoyaltyPoints).HasColumnType("decimal(18,2)");
        }
    }
}
