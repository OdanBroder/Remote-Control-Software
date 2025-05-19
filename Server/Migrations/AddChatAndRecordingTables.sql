-- Create ChatMessages table
CREATE TABLE ChatMessages (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    SessionId INT NOT NULL,
    SenderUserId INT NOT NULL,
    Message TEXT NOT NULL,
    MessageType VARCHAR(50) NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (SessionId) REFERENCES RemoteSessions(Id) ON DELETE CASCADE,
    FOREIGN KEY (SenderUserId) REFERENCES Users(InternalId) ON DELETE NO ACTION
);

-- Create SessionRecordings table
CREATE TABLE SessionRecordings (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    SessionId INT NOT NULL,
    StartedByUserId INT NOT NULL,
    FilePath VARCHAR(500) NOT NULL,
    Status VARCHAR(50) NOT NULL,
    ErrorMessage VARCHAR(500),
    StartedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    EndedAt DATETIME,
    FOREIGN KEY (SessionId) REFERENCES RemoteSessions(Id) ON DELETE CASCADE,
    FOREIGN KEY (StartedByUserId) REFERENCES Users(InternalId) ON DELETE NO ACTION
);

-- Create indexes
CREATE INDEX IX_ChatMessages_SessionId ON ChatMessages(SessionId);
CREATE INDEX IX_ChatMessages_SenderUserId ON ChatMessages(SenderUserId);
CREATE INDEX IX_SessionRecordings_SessionId ON SessionRecordings(SessionId);
CREATE INDEX IX_SessionRecordings_StartedByUserId ON SessionRecordings(StartedByUserId);
CREATE INDEX IX_SessionRecordings_Status ON SessionRecordings(Status); 