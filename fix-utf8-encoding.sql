-- Script para corrigir encoding UTF-8 das tabelas existentes
USE election_system;

-- Alterar charset das colunas da tabela elections
ALTER TABLE elections MODIFY COLUMN title VARCHAR(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE elections MODIFY COLUMN description TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- Alterar charset das colunas da tabela admins
ALTER TABLE admins MODIFY COLUMN name VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE admins MODIFY COLUMN email VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- Alterar charset das colunas da tabela positions
ALTER TABLE positions MODIFY COLUMN name VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE positions MODIFY COLUMN description TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- Alterar charset das colunas da tabela candidates
ALTER TABLE candidates MODIFY COLUMN name VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE candidates MODIFY COLUMN party VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE candidates MODIFY COLUMN biography TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- Alterar charset das colunas da tabela voters
ALTER TABLE voters MODIFY COLUMN name VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
ALTER TABLE voters MODIFY COLUMN email VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- Verificar se a correção funcionou
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    CHARACTER_SET_NAME,
    COLLATION_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'election_system'
    AND COLUMN_NAME IN ('title', 'description', 'name', 'email', 'party', 'biography')
ORDER BY TABLE_NAME, COLUMN_NAME;