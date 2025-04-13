using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models
{
    public class InputAction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int SessionId { get; set; }

        [Required]
        public required string Action { get; set; }

        public DateTime CreatedAt { get; set; }

        [ForeignKey("SessionId")]
        public RemoteSession Session { get; set; } = null!;
    }
}