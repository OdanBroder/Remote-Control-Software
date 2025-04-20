using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models
{
    public class RemoteSession
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public required string SessionIdentifier { get; set; }

        [Required]
        public int HostUserId { get; set; }

        [Required]
        public int ClientUserId { get; set; }

        [MaxLength(255)]
        public string? HostConnectionId { get; set; }

        [MaxLength(255)]
        public string? ClientConnectionId { get; set; }

        [Required]
        public string Status { get; set; } = "active";

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        [ForeignKey("HostUserId")]
        public User HostUser { get; set; } = null!;

        [ForeignKey("ClientUserId")]
        public User ClientUser { get; set; } = null!;
    }
}