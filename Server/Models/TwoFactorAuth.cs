using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models
{
    public class TwoFactorAuth
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public string SecretKey { get; set; } = string.Empty;

        [Required]
        public bool IsEnabled { get; set; }

        public string? BackupCodes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastUsed { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
    }
} 