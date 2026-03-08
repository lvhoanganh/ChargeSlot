using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
    {
        public void Configure(EntityTypeBuilder<Dispute> builder)
        {
            builder.ToTable("Dispute");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Reason).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            builder.Property(x => x.OwnerResponse).HasMaxLength(2000);
            builder.Property(x => x.AdminNote).HasMaxLength(2000);

            builder.HasOne(x => x.Booking)
                .WithOne(b => b.Dispute)
                .HasForeignKey<Dispute>(x => x.BookingId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.Invoice)
                .WithMany(i => i.Disputes)
                .HasForeignKey(x => x.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ResolvedByUserId: Admin Id=0, không FK vì Admin không lưu DB
            builder.Property(x => x.ResolvedByUserId);
        }
    }
}
