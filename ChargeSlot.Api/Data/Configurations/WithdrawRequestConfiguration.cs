using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class WithdrawRequestConfiguration : IEntityTypeConfiguration<WithdrawRequest>
    {
        public void Configure(EntityTypeBuilder<WithdrawRequest> builder)
        {
            builder.ToTable("WithdrawRequest");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Amount).HasPrecision(18, 2);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            builder.Property(x => x.BankName).HasMaxLength(200);
            builder.Property(x => x.BankAccountNumber).HasMaxLength(50);
            builder.Property(x => x.BankAccountHolder).HasMaxLength(200);
            builder.Property(x => x.AdminNote).HasMaxLength(2000);
            builder.Property(x => x.UserNote).HasMaxLength(2000);

            // Transfer fields
            builder.Property(x => x.TransferReceiptUrl).HasMaxLength(500);
            builder.Property(x => x.IssueNote).HasMaxLength(2000);

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Wallet)
                .WithMany()
                .HasForeignKey(x => x.WalletId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.UserId, x.RequestedAt });
            builder.HasIndex(x => x.Status);
        }
    }
}
