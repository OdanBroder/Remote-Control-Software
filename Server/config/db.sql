-- Drop the database if it exists
DROP DATABASE IF EXISTS RemoteControl_DB;

-- Create the database
CREATE DATABASE IF NOT EXISTS RemoteControl_DB
CHARACTER SET utf8mb4
COLLATE utf8mb4_unicode_ci;

-- Use the database
USE RemoteControl_DB;

-- Create the users table
CREATE TABLE IF NOT EXISTS Users (
    InternalId INT AUTO_INCREMENT PRIMARY KEY,          -- internal primary key
    Id CHAR(36) DEFAULT NULL UNIQUE,                    -- user-controlled ID (nullable initially)
    Username VARCHAR(100) NOT NULL UNIQUE,
    Password TEXT NOT NULL,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_username (Username),
    INDEX idx_user_id (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- Create the remote sessions table
CREATE TABLE IF NOT EXISTS RemoteSessions (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    SessionIdentifier VARCHAR(100) NOT NULL UNIQUE,
    HostUserId CHAR(36) NOT NULL,
    ClientUserId CHAR(36) DEFAULT NULL,
    HostConnectionId VARCHAR(255),
    ClientConnectionId VARCHAR(255),
    Status ENUM('active', 'ended') DEFAULT 'active',
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (HostUserId) REFERENCES Users(Id) ON DELETE CASCADE,
    FOREIGN KEY (ClientUserId) REFERENCES Users(Id) ON DELETE CASCADE,
    INDEX idx_session_identifier (SessionIdentifier),
    INDEX idx_host_user (HostUserId),
    INDEX idx_client_user (ClientUserId),
    INDEX idx_status (Status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Create the input actions table
CREATE TABLE IF NOT EXISTS InputActions (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    SessionIdentifier VARCHAR(100) NOT NULL,
    Action TEXT NOT NULL,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_session (SessionIdentifier)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Create a reference table for SignalType (unchanged from previous suggestion)
CREATE TABLE IF NOT EXISTS SignalTypes (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    TypeName VARCHAR(50) NOT NULL UNIQUE,
    Description TEXT,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Insert WebRTC-specific signal types
INSERT INTO SignalTypes (TypeName, Description) VALUES
('sdp_offer', 'WebRTC SDP offer for session initiation'),
('sdp_answer', 'WebRTC SDP answer for session response'),
('ice_candidate', 'WebRTC ICE candidate for P2P connection'),
('metadata', 'Screen-sharing metadata (e.g., resolution, frame rate)');

-- Create the revised ScreenData table
CREATE TABLE IF NOT EXISTS ScreenData (
    Id BIGINT AUTO_INCREMENT PRIMARY KEY, -- BIGINT for scalability
    SessionId INT NOT NULL, -- References RemoteSessions.Id
    SenderConnectionId VARCHAR(100) NOT NULL, -- WebRTC peer connection ID or WebSocket ID
    WebRTCConnectionId VARCHAR(100), -- Optional WebRTC connection ID
    SignalTypeId INT NOT NULL, -- References SignalTypes (e.g., sdp_offer, ice_candidate)
    SignalData JSON NOT NULL, -- Stores SDP, ICE candidates, or metadata as JSON
    FrameType ENUM('keyframe', 'delta') DEFAULT 'delta', -- Frame type for video data
    QualityLevel ENUM('low', 'medium', 'high') DEFAULT 'medium', -- Quality level for video data
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (SessionId) REFERENCES RemoteSessions(Id) ON DELETE CASCADE,
    FOREIGN KEY (SignalTypeId) REFERENCES SignalTypes(Id) ON DELETE RESTRICT,
    INDEX idx_session (SessionId),
    INDEX idx_sender (SenderConnectionId),
    INDEX idx_signal_type (SignalTypeId),
    INDEX idx_created_at (CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Stored procedure to clean up old ScreenData records
DELIMITER //
CREATE PROCEDURE IF NOT EXISTS CleanupOldScreenData()
BEGIN
    DELETE FROM ScreenData 
    WHERE CreatedAt < DATE_SUB(UTC_TIMESTAMP(), INTERVAL 30 DAY); -- Retain data for 30 days
END //
DELIMITER ;

-- Event to run the cleanup procedure daily
CREATE EVENT IF NOT EXISTS DailyScreenDataCleanup
ON SCHEDULE EVERY 1 DAY
DO CALL CleanupOldScreenData;

-- Create BlacklistedToken table
CREATE TABLE IF NOT EXISTS `BlacklistedTokens` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `Token` VARCHAR(1000) NOT NULL,
    `ExpiresAt` DATETIME NOT NULL,
    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `IX_BlacklistedTokens_Token` (`Token`(255))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Add index for faster token lookups
CREATE INDEX `IX_BlacklistedTokens_ExpiresAt` ON `BlacklistedTokens` (`ExpiresAt`);

-- Create a stored procedure to clean up expired tokens
DELIMITER //
CREATE PROCEDURE IF NOT EXISTS `CleanupExpiredTokens`()
BEGIN
    DELETE FROM `BlacklistedTokens` WHERE `ExpiresAt` < UTC_TIMESTAMP();
END //
DELIMITER ;

-- Create an event to run the cleanup procedure daily
CREATE EVENT IF NOT EXISTS `DailyTokenCleanup`
ON SCHEDULE EVERY 1 DAY
DO CALL `CleanupExpiredTokens`; 

-- Create FileTransfer table
CREATE TABLE IF NOT EXISTS `FileTransfers` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `SessionId` INT NOT NULL,
    `SenderUserId` CHAR(36) NOT NULL,
    `ReceiverUserId` CHAR(36) NOT NULL,
    `FileName` VARCHAR(255) NOT NULL,
    `FileSize` BIGINT NOT NULL,
    `Status` VARCHAR(50) NOT NULL,
    `ErrorMessage` TEXT,
    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`Id`),
    INDEX `IX_FileTransfers_Status` (`Status`),
    FOREIGN KEY (`SessionId`) REFERENCES `RemoteSessions` (`Id`) ON DELETE CASCADE,
    FOREIGN KEY (`SenderUserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE,
    FOREIGN KEY (`ReceiverUserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci; 

-- Create ChatMessages table
CREATE TABLE ChatMessages (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    SessionId INT NOT NULL,
    SenderUserId CHAR(36) NOT NULL,
    Message TEXT NOT NULL,
    MessageType VARCHAR(50) NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (SessionId) REFERENCES RemoteSessions(Id) ON DELETE CASCADE,
    FOREIGN KEY (SenderUserId) REFERENCES Users(Id) ON DELETE NO ACTION
);

-- Create SessionRecordings table
CREATE TABLE SessionRecordings (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    SessionId INT NOT NULL,
    StartedByUserId CHAR(36) NOT NULL,
    FilePath VARCHAR(500) NOT NULL,
    Status VARCHAR(50) NOT NULL,
    ErrorMessage VARCHAR(500),
    StartedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    EndedAt DATETIME,
    FOREIGN KEY (SessionId) REFERENCES RemoteSessions(Id) ON DELETE CASCADE,
    FOREIGN KEY (StartedByUserId) REFERENCES Users(Id) ON DELETE NO ACTION
);

-- Create indexes
CREATE INDEX IX_ChatMessages_SessionId ON ChatMessages(SessionId);
CREATE INDEX IX_ChatMessages_SenderUserId ON ChatMessages(SenderUserId);
CREATE INDEX IX_SessionRecordings_SessionId ON SessionRecordings(SessionId);
CREATE INDEX IX_SessionRecordings_StartedByUserId ON SessionRecordings(StartedByUserId);
CREATE INDEX IX_SessionRecordings_Status ON SessionRecordings(Status); 

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
    UserId CHAR(36) NOT NULL,
    Action VARCHAR(100) NOT NULL,
    Details TEXT NOT NULL,
    IpAddress VARCHAR(50) NOT NULL,
    Timestamp DATETIME NOT NULL,
    FOREIGN KEY (SessionId) REFERENCES RemoteSessions(Id) ON DELETE CASCADE,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE NO ACTION
);

-- Create indexes
CREATE INDEX IX_MonitorInfos_SessionId ON MonitorInfos(SessionId);
CREATE INDEX IX_SessionStatistics_SessionId ON SessionStatistics(SessionId);
CREATE INDEX IX_SessionStatistics_Timestamp ON SessionStatistics(Timestamp);
CREATE INDEX IX_SessionAuditLogs_SessionId ON SessionAuditLogs(SessionId);
CREATE INDEX IX_SessionAuditLogs_UserId ON SessionAuditLogs(UserId);
CREATE INDEX IX_SessionAuditLogs_Timestamp ON SessionAuditLogs(Timestamp); 

-- Create TwoFactorAuths table
CREATE TABLE TwoFactorAuths (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    UserId INT NOT NULL,
    SecretKey VARCHAR(100) NOT NULL,
    IsEnabled BOOLEAN NOT NULL,
    BackupCodes TEXT,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    LastUsed DATETIME,
    FOREIGN KEY (UserId) REFERENCES Users(InternalId) ON DELETE CASCADE
);

-- Create IpWhitelists table
CREATE TABLE IpWhitelists (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    UserId INT NOT NULL,
    IpAddress VARCHAR(50) NOT NULL,
    Description VARCHAR(200) NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    LastUsed DATETIME,
    FOREIGN KEY (UserId) REFERENCES Users(InternalId) ON DELETE CASCADE
);

-- Create indexes
CREATE INDEX IX_TwoFactorAuths_UserId ON TwoFactorAuths(UserId);
CREATE INDEX IX_IpWhitelists_UserId ON IpWhitelists(UserId);
CREATE INDEX IX_IpWhitelists_IpAddress ON IpWhitelists(IpAddress); 

-- Create WebRTCConnections table
CREATE TABLE IF NOT EXISTS WebRTCConnections (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    SessionId INT NOT NULL,
    ConnectionId VARCHAR(100) NOT NULL,
    ConnectionType ENUM('host', 'client') NOT NULL,
    IceCandidates TEXT,
    Offer TEXT,
    Answer TEXT,
    Status ENUM('pending', 'connected', 'disconnected') DEFAULT 'pending',
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (SessionId) REFERENCES RemoteSessions(Id) ON DELETE CASCADE,
    INDEX idx_session (SessionId),
    INDEX idx_connection (ConnectionId),
    INDEX idx_status (Status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Create WebRTCStats table
CREATE TABLE IF NOT EXISTS WebRTCStats (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    SessionId INT NOT NULL,
    ConnectionId VARCHAR(100) NOT NULL,
    BytesReceived BIGINT NOT NULL DEFAULT 0,
    BytesSent BIGINT NOT NULL DEFAULT 0,
    PacketsLost INT NOT NULL DEFAULT 0,
    RoundTripTime FLOAT,
    Jitter FLOAT,
    Timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (SessionId) REFERENCES RemoteSessions(Id) ON DELETE CASCADE,
    INDEX idx_session (SessionId),
    INDEX idx_connection (ConnectionId),
    INDEX idx_timestamp (Timestamp)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci; 

-- Create the input errors table
CREATE TABLE IF NOT EXISTS `InputErrors` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `UserId` CHAR(36) NOT NULL,
    `ErrorData` TEXT NOT NULL,
    `CreatedAt` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX `idx_user_id` (`UserId`),
    INDEX `idx_created_at` (`CreatedAt`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci; 