using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models
{
    public class FileTransfer
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int SessionId { get; set; }

        [Required]
        public int SenderUserId { get; set; }

        [Required]
        public int ReceiverUserId { get; set; }

        [Required]
        [MaxLength(255)]
        public required string FileName { get; set; }

        [Required]
        public long FileSize { get; set; }

        [Required]
        public string Status { get; set; } = "pending"; // pending, transferring, completed, failed

        public string? ErrorMessage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("SessionId")]
        public RemoteSession Session { get; set; } = null!;

        [ForeignKey("SenderUserId")]
        public User Sender { get; set; } = null!;

        [ForeignKey("ReceiverUserId")]
        public User Receiver { get; set; } = null!;
    }
} 