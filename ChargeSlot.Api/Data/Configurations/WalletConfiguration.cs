using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
    {
        public void Configure(EntityTypeBuilder<Wallet> builder)
        {
            builder.ToTable("Wallet");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.WalletType).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.SystemCode).HasMaxLength(50);
            builder.Property(x => x.AvailableBalance).HasPrecision(18, 2);
            builder.Property(x => x.FrozenBalance).HasPrecision(18, 2);
            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.WalletType, x.UserId }).IsUnique().HasFilter("[UserId] IS NOT NULL");
            builder.HasIndex(x => x.SystemCode).IsUnique().HasFilter("[SystemCode] IS NOT NULL");
        }
    }
}
