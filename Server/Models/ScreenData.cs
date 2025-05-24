using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Server.Models
{
    public class ScreenData
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        public int SessionId { get; set; }

        [Required]
        [Column(TypeName = "varchar(100)")]
        public string SenderConnectionId { get; set; } = string.Empty;

        [Column(TypeName = "varchar(100)")]
        public string? WebRTCConnectionId { get; set; }

        [Required]
        public int SignalTypeId { get; set; }

        [Required]
        [Column(TypeName = "json")]
        public string SignalData { get; set; } = "{}";

        [Column(TypeName = "enum('keyframe', 'delta')")]
        public string FrameType { get; set; } = "delta";

        [Column(TypeName = "enum('low', 'medium', 'high')")]
        public string QualityLevel { get; set; } = "medium";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("SessionId")]
        public RemoteSession? Session { get; set; }

        [ForeignKey("SignalTypeId")]
        public SignalType? SignalType { get; set; }

        // Helper method to get typed signal data
        public T? GetSignalData<T>() where T : class
        {
            try
            {
                return JsonSerializer.Deserialize<T>(SignalData);
            }
            catch
            {
                return null;
            }
        }

        // Helper method to set signal data
        public void SetSignalData<T>(T data) where T : class
        {
            SignalData = JsonSerializer.Serialize(data);
        }
    }
}