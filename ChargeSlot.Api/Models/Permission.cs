using ChargeSlot.Api.Models.Identity;

namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 Permission - RBAC, e.g. CREATE_BOOKING, APPROVE_STATION.</summary>
    public class Permission
    {
        public int Id { get; set; }
        public string Code { get; set; } = null!;
        public string? Description { get; set; }

        public ICollection<IdentityRolePermission> RolePermissions { get; set; } = new List<IdentityRolePermission>();
    }
}
