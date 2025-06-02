using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models
{
    public class WebRTCConnection
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int SessionId { get; set; }

        [Required]
        [Column(TypeName = "varchar(100)")]
        public string ConnectionId { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "enum('host', 'client')")]
        public string ConnectionType { get; set; } = "client";

        [Column(TypeName = "text")]
        public string? IceCandidates { get; set; }

        [Column(TypeName = "text")]
        public string? Offer { get; set; }

        [Column(TypeName = "text")]
        public string? Answer { get; set; }

        [Required]
        [Column(TypeName = "enum('pending', 'connected', 'disconnected')")]
        public string Status { get; set; } = "pending";

        [Required]
        [Column(TypeName = "datetime")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [Column(TypeName = "datetime")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("SessionId")]
        public RemoteSession? Session { get; set; }
    }
} 