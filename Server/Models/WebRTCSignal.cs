using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Server.Models
{
    public class WebRTCSignal
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string SessionIdentifier { get; set; } = null!;

        [Required]
        public string SenderConnectionId { get; set; } = null!;

        [Required]
        public string SignalType { get; set; } = null!;

        [Required]
        public JsonDocument SignalData { get; set; } = null!;

        [Required]
        public DateTime CreatedAt { get; set; }

        [ForeignKey("SessionIdentifier")]
        public RemoteSession Session { get; set; } = null!;
    }
} 