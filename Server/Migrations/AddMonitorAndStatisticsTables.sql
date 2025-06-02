-- Create MonitorInfos table
CREATE TABLE MonitorInfos (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    SessionId INT NOT NULL,
    MonitorIndex INT NOT NULL,
    DeviceName VARCHAR(100) NOT NULL,
    Width INT NOT NULL,
    Height INT NOT NULL,
    RefreshRate INT NOT NULL,
    IsPrimary BOOLEAN NOT NULL,
    X INT NOT NULL,
    Y INT NOT NULL,
    FOREIGN KEY (SessionId) REFERENCES RemoteSessions(Id) ON DELETE CASCADE
);

-- Create SessionStatistics table
CREATE TABLE SessionStatistics (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    SessionId INT NOT NULL,
    Timestamp DATETIME NOT NULL,
    BandwidthUsage FLOAT NOT NULL,
    FrameRate INT NOT NULL,
    Latency FLOAT NOT NULL,
    PacketLoss FLOAT NOT NULL,
    QualityLevel VARCHAR(50) NOT NULL,
    CompressionLevel VARCHAR(50) NOT NULL,
    FOREIGN KEY (SessionId) REFERENCES RemoteSessions(Id) ON DELETE CASCADE
);

-- Create SessionAuditLogs table
CREATE TABLE SessionAuditLogs (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    SessionId INT NOT NULL,
    UserId INT NOT NULL,
    Action VARCHAR(100) NOT NULL,
    Details TEXT NOT NULL,
    IpAddress VARCHAR(50) NOT NULL,
    Timestamp DATETIME NOT NULL,
    FOREIGN KEY (SessionId) REFERENCES RemoteSessions(Id) ON DELETE CASCADE,
    FOREIGN KEY (UserId) REFERENCES Users(InternalId) ON DELETE NO ACTION
);

-- Create indexes
CREATE INDEX IX_MonitorInfos_SessionId ON MonitorInfos(SessionId);
CREATE INDEX IX_SessionStatistics_SessionId ON SessionStatistics(SessionId);
CREATE INDEX IX_SessionStatistics_Timestamp ON SessionStatistics(Timestamp);
CREATE INDEX IX_SessionAuditLogs_SessionId ON SessionAuditLogs(SessionId);
CREATE INDEX IX_SessionAuditLogs_UserId ON SessionAuditLogs(UserId);
CREATE INDEX IX_SessionAuditLogs_Timestamp ON SessionAuditLogs(Timestamp); 