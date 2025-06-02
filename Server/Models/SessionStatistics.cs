using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models
{
    public class SessionStatistics
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int SessionId { get; set; }

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [Required]
        public double BandwidthUsage { get; set; } // in Mbps

        [Required]
        public int FrameRate { get; set; }

        [Required]
        public double Latency { get; set; } // in milliseconds

        [Required]
        public double PacketLoss { get; set; } // percentage

        [Required]
        public string QualityLevel { get; set; } = "medium"; // low, medium, high

        [Required]
        public string CompressionLevel { get; set; } = "medium"; // low, medium, high

        [ForeignKey("SessionId")]
        public RemoteSession Session { get; set; } = null!;
    }
} 