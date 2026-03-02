using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class BookingExtraServiceConfiguration : IEntityTypeConfiguration<BookingExtraService>
    {
        public void Configure(EntityTypeBuilder<BookingExtraService> builder)
        {
            builder.ToTable("BookingExtraService");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
            builder.Property(x => x.TotalPrice).HasPrecision(18, 2);
            builder.HasOne(x => x.Booking)
                .WithMany(b => b.BookingExtraServices)
                .HasForeignKey(x => x.BookingId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.ExtraService)
                .WithMany(s => s.BookingExtraServices)
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => x.BookingId);
        }
    }
}
