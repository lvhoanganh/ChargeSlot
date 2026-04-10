using ChargeSlot.Api.Models.Identity;

using ChargeSlot.Api.Helpers;
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
        public DateTime CreatedAt { get; set; } = DateTimeHelper.VietnamNow();

        /// <summary>Null if still active; set when revoked or rotated.</summary>
        public DateTime? RevokedAt { get; set; }

        /// <summary>The new token that replaced this one (token rotation).</summary>
        public string? ReplacedByToken { get; set; }

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool IsRevoked => RevokedAt != null;
        public bool IsActive => !IsRevoked && !IsExpired;
    }
}
