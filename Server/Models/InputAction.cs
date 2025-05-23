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
        public string SessionIdentifier { get; set; } = string.Empty;

        [Required]
        public string Action { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}