using ChargeSlot.Api.Models.Identity;

namespace ChargeSlot.Api.Models
{
    public class RefreshToken
    {
        public int Id { get; set; }

        /// <summary>Random 64-byte base64 string.</summary>
        public string Token { get; set; } = null!;

        public int UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Null if still active; set when revoked or rotated.</summary>
        public DateTime? RevokedAt { get; set; }

        /// <summary>The new token that replaced this one (token rotation).</summary>
        public string? ReplacedByToken { get; set; }

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool IsRevoked => RevokedAt != null;
        public bool IsActive => !IsRevoked && !IsExpired;
    }
}
