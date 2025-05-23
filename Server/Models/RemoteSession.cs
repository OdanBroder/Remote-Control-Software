using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models
{
    public class RemoteSession
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string SessionIdentifier { get; set; } = null!;

        [Required]
        public Guid HostUserId { get; set; }

        public Guid? ClientUserId { get; set; }

        [Required]
        public string Status { get; set; } = "active";

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string? HostConnectionId { get; set; }

        public string? ClientConnectionId { get; set; }

        [ForeignKey("HostUserId")]
        public User HostUser { get; set; } = null!;

        [ForeignKey("ClientUserId")]
        public User? ClientUser { get; set; }
    }
}