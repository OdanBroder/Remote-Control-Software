using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models
{
    public class SessionAuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int SessionId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public string Action { get; set; } = string.Empty;

        [Required]
        public string Details { get; set; } = string.Empty;

        [Required]
        public string IpAddress { get; set; } = string.Empty;

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [ForeignKey("SessionId")]
        public RemoteSession Session { get; set; } = null!;

        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
    }
} 