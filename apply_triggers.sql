-- Script para aplicar triggers manualmente no banco election_system

USE election_system;

-- Remove triggers existentes se houver
DROP TRIGGER IF EXISTS tr_election_auto_assign_company;
DROP TRIGGER IF EXISTS tr_election_auto_update_seal;

-- Trigger para atualizar company_id automaticamente ao criar eleição
DELIMITER $$
CREATE TRIGGER tr_election_auto_assign_company
BEFORE INSERT ON elections
FOR EACH ROW
BEGIN
    -- Se company_id não foi especificado, pega a primeira company ativa
    IF NEW.company_id IS NULL THEN
        SET NEW.company_id = (
            SELECT id 
            FROM companies 
            WHERE is_active = 1 
            ORDER BY created_at ASC 
            LIMIT 1
        );
    END IF;
END$$
DELIMITER ;

-- Trigger para atualizar campos de seal automaticamente
DELIMITER $$
CREATE TRIGGER tr_election_auto_update_seal
BEFORE UPDATE ON elections
FOR EACH ROW
BEGIN
    -- Se is_sealed mudou de false para true, atualiza os campos de seal
    IF OLD.is_sealed = 0 AND NEW.is_sealed = 1 THEN
        -- Busca o hash mais recente da system_seals para esta eleição
        SELECT seal_hash, sealed_at, sealed_by
        INTO NEW.seal_hash, NEW.sealed_at, NEW.sealed_by
        FROM system_seals
        WHERE election_id = NEW.id
        AND seal_type = 'ELECTION_SEAL'
        ORDER BY sealed_at DESC
        LIMIT 1;
        
        -- Se não encontrou dados na system_seals, define valores padrão
        IF NEW.seal_hash IS NULL THEN
            SET NEW.sealed_at = NOW();
        END IF;
    END IF;
END$$
DELIMITER ;

-- Verificar se os triggers foram criados
SHOW TRIGGERS;