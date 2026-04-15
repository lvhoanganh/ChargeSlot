using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class ContractConfiguration : IEntityTypeConfiguration<Contract>
    {
        public void Configure(EntityTypeBuilder<Contract> builder)
        {
            builder.ToTable("Contract");
            builder.HasKey(x => x.Id);

            builder.HasOne(x => x.Owner)
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(x => x.ContractNumber).HasMaxLength(50).IsRequired();
            builder.Property(x => x.OwnerName).HasMaxLength(255).IsRequired();
            builder.Property(x => x.OwnerIdCard).HasMaxLength(50).IsRequired();
            builder.Property(x => x.OwnerTaxCode).HasMaxLength(100).IsRequired();
            builder.Property(x => x.OwnerAddress).HasMaxLength(500).IsRequired();
            builder.Property(x => x.OwnerBusinessLicense).HasMaxLength(255).IsRequired();
            builder.Property(x => x.OwnerPhone).HasMaxLength(20).IsRequired();
            builder.Property(x => x.OwnerEmail).HasMaxLength(255).IsRequired();
            builder.Property(x => x.SignatureImageUrl).HasMaxLength(1000);
            builder.Property(x => x.SignedPdfUrl).HasMaxLength(1000);

            builder.HasIndex(x => x.OwnerUserId);
            builder.HasIndex(x => x.ContractNumber).IsUnique();
        }
    }
}
