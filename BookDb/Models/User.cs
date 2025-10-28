using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace BookDb.Models
{
    public class User : IdentityUser
    {
        [MaxLength(200)]
        public string? FullName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginAt { get; set; }

        public bool IsActive { get; set; } = true;
    }
}