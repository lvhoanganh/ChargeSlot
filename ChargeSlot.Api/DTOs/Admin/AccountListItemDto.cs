

namespace ChargeSlot.Api.DTOs.Admin
{
    public class AccountListItemDto
    {
        public int Id { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }      // Driver / Owner / Admin
        public string? Status { get; set; }    // ACTIVE | BANNED | SUSPENDED | ...
        public int BanCount { get; set; }
        public DateTime? BannedUntil { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

