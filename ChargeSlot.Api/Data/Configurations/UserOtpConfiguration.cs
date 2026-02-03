using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class UserOtpConfiguration : IEntityTypeConfiguration<UserOtp>
    {
        public void Configure(EntityTypeBuilder<UserOtp> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.PhoneNumber)
                   .IsRequired()
                   .HasMaxLength(20);

            builder.Property(x => x.OtpHash)
                   .IsRequired();

            // 🔥 INDEX QUAN TRỌNG: tìm OTP theo phone nhanh
            builder.HasIndex(x => x.PhoneNumber);

            // (Optional – nâng cao)
            // Giúp lọc OTP còn hiệu lực nhanh hơn
            builder.HasIndex(x => new { x.PhoneNumber, x.IsUsed, x.ExpiredAt });
        }
    }
}
