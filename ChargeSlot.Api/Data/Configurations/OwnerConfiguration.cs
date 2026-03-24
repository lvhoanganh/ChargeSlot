using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class OwnerConfiguration : IEntityTypeConfiguration<Owner>
    {
        public void Configure(EntityTypeBuilder<Owner> builder)
        {
            builder.ToTable("Owner");
            builder.HasKey(x => x.UserId);
            builder.HasOne(x => x.User)
                .WithOne(u => u.OwnerProfile)
                .HasForeignKey<Owner>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Property(x => x.BusinessName).HasMaxLength(255).IsRequired();
            builder.Property(x => x.TaxCode).HasMaxLength(100).IsRequired();
        }
    }
}
