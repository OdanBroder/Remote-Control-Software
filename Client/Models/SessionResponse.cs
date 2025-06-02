using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Models
{
    public class SessionResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Code { get; set; }
        public SessionData Data { get; set; }

    }

    public class SessionData
    {
        public string SessionId { get; set; }
    }
}
