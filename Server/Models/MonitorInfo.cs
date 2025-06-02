using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Models
{
    public class MonitorInfo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int SessionId { get; set; }

        [Required]
        public int MonitorIndex { get; set; }

        [Required]
        public string DeviceName { get; set; } = string.Empty;

        [Required]
        public int Width { get; set; }

        [Required]
        public int Height { get; set; }

        [Required]
        public int RefreshRate { get; set; }

        [Required]
        public bool IsPrimary { get; set; }

        public int X { get; set; }
        public int Y { get; set; }

        [ForeignKey("SessionId")]
        public RemoteSession Session { get; set; } = null!;
    }
} 