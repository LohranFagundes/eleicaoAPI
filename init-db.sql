-- ===============================================
-- Election API Database Initialization Script
-- Version: 1.1.1
-- Updated: 02/08/2024
-- ===============================================

-- Set charset and collation for better Unicode support
ALTER DATABASE election_system CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- Create user if not exists (for compatibility)
CREATE USER IF NOT EXISTS 'root'@'%' IDENTIFIED BY 'ElectionSystem2024!';
GRANT ALL PRIVILEGES ON election_system.* TO 'root'@'%';

-- Optimize MySQL settings for the application
SET GLOBAL innodb_buffer_pool_size = 268435456; -- 256MB
SET GLOBAL max_connections = 500;
SET GLOBAL innodb_log_file_size = 134217728; -- 128MB

-- Set timezone
SET GLOBAL time_zone = '-03:00'; -- Brazil timezone

-- Log initialization
INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) 
VALUES ('00000000000000_InitialSetup', '8.0.0');

-- Success message
SELECT 'Election API Database initialized successfully!' as Status,
       'Version 1.1.1' as Version,
       '2024-08-02' as UpdateDate,
       'Ready for Two-Factor Auth, System Seals, and Hybrid Photos' as Features;