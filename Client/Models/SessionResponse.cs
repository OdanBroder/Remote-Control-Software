using System.Collections.Generic;
using System;
public class StartSessionResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string Code { get; set; }
    public SessionData Data { get; set; }
}
public class SessionResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string Code { get; set; }
    public List<SessionData> Data { get; set; }
}

public class SessionData
{
    public string SessionId { get; set; }
    public string HostUsername { get; set; }
    public string ClientUsername { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivity { get; set; }
    public string Status { get; set; }
}
