using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models
{
    public class WebRTCStats
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
        public long BytesReceived { get; set; }

        [Required]
        public long BytesSent { get; set; }

        [Required]
        public int PacketsLost { get; set; }

        public float? RoundTripTime { get; set; }

        public float? Jitter { get; set; }

        [Required]
        [Column(TypeName = "datetime")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [ForeignKey("SessionId")]
        public RemoteSession? Session { get; set; }
    }
} 