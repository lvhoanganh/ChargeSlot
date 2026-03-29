using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class ChatConversationConfiguration : IEntityTypeConfiguration<ChatConversation>
    {
        public void Configure(EntityTypeBuilder<ChatConversation> builder)
        {
            builder.HasKey(c => c.Id);

            // 1 booking → tối đa 1 conversation
            builder.HasIndex(c => c.BookingId).IsUnique();

            builder.HasOne(c => c.Booking)
                .WithOne()
                .HasForeignKey<ChatConversation>(c => c.BookingId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(c => c.Driver)
                .WithMany()
                .HasForeignKey(c => c.DriverUserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(c => c.Owner)
                .WithMany()
                .HasForeignKey(c => c.OwnerUserId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
