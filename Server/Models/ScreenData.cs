using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models
{
    public class ScreenData
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int SessionId { get; set; }

        [NotMapped]
        public string SessionIdentifier { get; set; } = string.Empty;

        [Required]
        public string Data { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        [ForeignKey("SessionId")]
        public RemoteSession? Session { get; set; }
    }
}