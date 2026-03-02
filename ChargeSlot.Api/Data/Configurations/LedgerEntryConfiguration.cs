using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
    {
        public void Configure(EntityTypeBuilder<LedgerEntry> builder)
        {
            builder.ToTable("LedgerEntry");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Direction).HasConversion<string>().HasMaxLength(10);
            builder.Property(x => x.Amount).HasPrecision(18, 2);
            builder.HasOne(x => x.LedgerTransaction)
                .WithMany(t => t.Entries)
                .HasForeignKey(x => x.LedgerTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.Wallet)
                .WithMany(w => w.LedgerEntries)
                .HasForeignKey(x => x.WalletId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.WalletId, x.CreatedAt });
            builder.HasIndex(x => x.LedgerTransactionId);
        }
    }
}
