using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models
{
    public class SessionActivityLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string SessionIdentifier { get; set; } = null!;

        [Required]
        public Guid UserId { get; set; }

        [Required]
        public string ActivityType { get; set; } = null!;

        public string? Description { get; set; }

        public string? IpAddress { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        [ForeignKey("SessionIdentifier")]
        public RemoteSession Session { get; set; } = null!;

        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
    }
} 