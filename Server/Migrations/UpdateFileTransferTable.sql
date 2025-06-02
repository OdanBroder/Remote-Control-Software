-- Add missing columns to FileTransfers table
ALTER TABLE `FileTransfers`
    ADD COLUMN `CompletedAt` DATETIME NULL,
    MODIFY COLUMN `SenderUserId` CHAR(36) NOT NULL,
    MODIFY COLUMN `ReceiverUserId` CHAR(36) NOT NULL; 