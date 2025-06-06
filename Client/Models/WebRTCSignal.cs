using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#nullable enable

namespace Client.Models
{
    public class WebRTCSignal
    {
        public string? SessionIdentifier { get; set; }
        public string? ConnectionId { get; set; }
        public string? SignalType { get; set; }  // "offer", "answer", "ice-candidate"
        public object? SignalData { get; set; }
        public object? Content { get; set; }
        public string? Sdp { get; set; } // Only for offer/answer
        public string? Candidate { get; set; } // Only for ice-candidate
        public string? SdpMid { get; set; }
        public int? SdpMLineIndex { get; set; }
    }
}
