using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
    {
        public void Configure(EntityTypeBuilder<Invoice> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.TotalAmount)
                   .HasPrecision(18, 2);

            builder.HasOne(x => x.Booking)
                   .WithOne(b => b.Invoice)
                   .HasForeignKey<Invoice>(x => x.BookingId);
        }
    }
}
