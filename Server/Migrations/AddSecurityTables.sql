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