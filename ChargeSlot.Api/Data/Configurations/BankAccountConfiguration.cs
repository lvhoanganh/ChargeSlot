using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
    {
        public void Configure(EntityTypeBuilder<BankAccount> builder)
        {
            builder.ToTable("BankAccount");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.BankName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.BankAccountNumber).HasMaxLength(100).IsRequired();
            builder.Property(x => x.BankAccountHolder).HasMaxLength(150).IsRequired();
            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.UserId, x.IsDefault });
        }
    }
}
