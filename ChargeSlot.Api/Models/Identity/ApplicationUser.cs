using Microsoft.AspNetCore.Identity;

namespace ChargeSlot.Api.Models.Identity
{
    public class ApplicationUser : IdentityUser<int>
    {
        public string? FullName { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


    }
}
