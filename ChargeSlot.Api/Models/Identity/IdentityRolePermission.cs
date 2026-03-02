using Microsoft.AspNetCore.Identity;

namespace ChargeSlot.Api.Models.Identity
{
    /// <summary>Role-Permission many-to-many (SRS 1.5 RolePermission).</summary>
    public class IdentityRolePermission
    {
        public int RoleId { get; set; }
        public IdentityRole<int> Role { get; set; } = null!;

        public int PermissionId { get; set; }
        public Permission Permission { get; set; } = null!;
    }
}
