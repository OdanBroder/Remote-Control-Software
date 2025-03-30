using System.Collections.Generic;
using Server.Models;
using System.Linq;

namespace Server.Services
{
    public class RemoteSessionService
    {
        private readonly List<RemoteSession> _sessions = new();
        public void CreateSession(RemoteSession session) => _sessions.Add(session);
        public RemoteSession GetSession(string id) => _sessions.FirstOrDefault(s => s.Id == id);
    }
}