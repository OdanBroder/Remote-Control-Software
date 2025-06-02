-- Create FileTransfer table
CREATE TABLE IF NOT EXISTS `FileTransfers` (
    `Id` INT NOT NULL AUTO_INCREMENT,
    `SessionId` INT NOT NULL,
    `SenderUserId` INT NOT NULL,
    `ReceiverUserId` INT NOT NULL,
    `FileName` VARCHAR(255) NOT NULL,
    `FileSize` BIGINT NOT NULL,
    `Status` VARCHAR(50) NOT NULL,
    `ErrorMessage` TEXT,
    `CreatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedAt` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`Id`),
    INDEX `IX_FileTransfers_Status` (`Status`),
    FOREIGN KEY (`SessionId`) REFERENCES `RemoteSessions` (`Id`) ON DELETE CASCADE,
    FOREIGN KEY (`SenderUserId`) REFERENCES `Users` (`InternalId`) ON DELETE CASCADE,
    FOREIGN KEY (`ReceiverUserId`) REFERENCES `Users` (`InternalId`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci; 