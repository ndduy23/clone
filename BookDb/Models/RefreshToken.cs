using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookDb.Models
{
    public class RefreshToken
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string UserId { get; set; } = string.Empty;
        [ForeignKey("UserId")]
        public User? User { get; set; }
        [Required]
        [MaxLength(500)]
        public string Token { get; set; } = string.Empty;
        [MaxLength(100)]
        public string? JwtId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; } = false;
        public bool IsRevoked { get; set; } = false;
        [MaxLength(500)]
        public string? DeviceInfo { get; set; }
        [MaxLength(50)]
        public string? IpAddress { get; set; }
        public DateTime? UsedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        [MaxLength(500)]
        public string? RevokeReason { get; set; }
        public bool IsActive =>
            !IsRevoked &&
            !IsUsed &&
            DateTime.UtcNow < ExpiresAt;
    }
}