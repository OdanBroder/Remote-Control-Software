using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Models
{
    public class WebRTCSignal
    {
        public required string SessionIdentifier { get; set; }
        public required string ConnectionId { get; set; }
        public required string SignalType { get; set; }  // "offer", "answer", "ice-candidate"
        public object? SignalData { get; set; }
        public object? Content { get; set; }
        public string? Sdp { get; set; } // Only for offer/answer
        public string? Candidate { get; set; } // Only for ice-candidate
        public string? SdpMid { get; set; }
        public int? SdpMLineIndex { get; set; }
        
    }
}
