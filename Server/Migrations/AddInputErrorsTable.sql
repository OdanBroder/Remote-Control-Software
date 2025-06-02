-- Create the input errors table
CREATE TABLE IF NOT EXISTS `InputErrors` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `UserId` CHAR(36) NOT NULL,
    `ErrorData` TEXT NOT NULL,
    `CreatedAt` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX `idx_user_id` (`UserId`),
    INDEX `idx_created_at` (`CreatedAt`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci; 