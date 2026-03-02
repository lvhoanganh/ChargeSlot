using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.ToTable("Notification");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
            builder.Property(x => x.Content).IsRequired();
            builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.UserId, x.CreatedAt });
        }
    }
}
