using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class LoyaltyTransactionConfiguration : IEntityTypeConfiguration<LoyaltyTransaction>
    {
        public void Configure(EntityTypeBuilder<LoyaltyTransaction> builder)
        {
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Type).HasMaxLength(20).IsRequired();
            builder.Property(t => t.Points).HasColumnType("decimal(18,2)");
            builder.Property(t => t.Description).HasMaxLength(500);

            builder.HasOne(t => t.Driver)
                .WithMany()
                .HasForeignKey(t => t.DriverUserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(t => t.Booking)
                .WithMany()
                .HasForeignKey(t => t.BookingId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
