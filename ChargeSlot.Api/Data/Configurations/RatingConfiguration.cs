using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class RatingConfiguration : IEntityTypeConfiguration<Rating>
    {
        public void Configure(EntityTypeBuilder<Rating> builder)
        {
            builder.ToTable("Rating");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Comment).HasMaxLength(2000);
            builder.HasOne(x => x.Booking)
                .WithOne(b => b.Rating)
                .HasForeignKey<Rating>(x => x.BookingId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.DriverUser)
                .WithMany()
                .HasForeignKey(x => x.DriverUserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.ChargingStation)
                .WithMany()
                .HasForeignKey(x => x.StationId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.StationId, x.CreatedAt });
        }
    }
}
