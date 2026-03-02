using ChargeSlot.Api.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChargeSlot.Api.Data.Configurations
{
    public class IdentityRolePermissionConfiguration : IEntityTypeConfiguration<IdentityRolePermission>
    {
        public void Configure(EntityTypeBuilder<IdentityRolePermission> builder)
        {
            builder.ToTable("RolePermission");
            builder.HasKey(x => new { x.RoleId, x.PermissionId });
            builder.HasOne(x => x.Role)
                .WithMany()
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
