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
    ClientUserId CHAR(36) NOT NULL,
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
    SessionId INT NOT NULL,
    SessionIdentifier VARCHAR(100) NOT NULL,
    Action TEXT NOT NULL,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (SessionId) REFERENCES RemoteSessions(Id) ON DELETE CASCADE,
    INDEX idx_session (SessionId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Create the screen data table
CREATE TABLE IF NOT EXISTS ScreenData (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    SessionId INT NOT NULL,
    Data LONGTEXT NOT NULL,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (SessionId) REFERENCES RemoteSessions(Id) ON DELETE CASCADE,
    INDEX idx_session (SessionId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
