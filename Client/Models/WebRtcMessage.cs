using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Models
{
    public class WebRtcMessage
    {
        public string Type { get; set; } // offer, answer, ice-candidate
        public object Conetent { get; set; }
        public string Sdp { get; set; } // Only for offer/answer
        public string Candidate { get; set; } // Only for ice-candidate
        public string SdpMid { get; set; }
        public int? SdpMLineIndex { get; set; }
        public string FromConnectionId { get; set; }
    }
}
