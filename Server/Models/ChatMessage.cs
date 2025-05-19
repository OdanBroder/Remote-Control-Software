using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models
{
    public class ChatMessage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int SessionId { get; set; }

        [Required]
        public int SenderUserId { get; set; }

        [Required]
        [MaxLength(1000)]
        public required string Message { get; set; }

        [Required]
        public string MessageType { get; set; } = "text"; // text, system, file

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("SessionId")]
        public RemoteSession Session { get; set; } = null!;

        [ForeignKey("SenderUserId")]
        public User Sender { get; set; } = null!;
    }
} 