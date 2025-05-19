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