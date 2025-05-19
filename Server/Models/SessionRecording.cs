using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models
{
    public class SessionRecording
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int SessionId { get; set; }

        [Required]
        public int StartedByUserId { get; set; }

        [Required]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        public string Status { get; set; } = "recording"; // recording, completed, failed

        public string? ErrorMessage { get; set; }

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? EndedAt { get; set; }

        [ForeignKey("SessionId")]
        public RemoteSession Session { get; set; } = null!;

        [ForeignKey("StartedByUserId")]
        public User StartedBy { get; set; } = null!;
    }
} 