-- ===============================================================================
-- TRIGGER PARA AUTO-LACRE DE ELEIÇÕES
-- ===============================================================================
-- 
-- DESCRIÇÃO:
-- Este trigger é executado automaticamente sempre que a tabela 'elections' 
-- for atualizada. Ele monitora mudanças na coluna 'is_sealed' e atualiza
-- automaticamente as colunas relacionadas ao lacre quando uma eleição é selada.
--
-- FUNCIONALIDADES:
-- 1. Detecta quando is_sealed muda de FALSE para TRUE
-- 2. Gera automaticamente o seal_hash (SHA2-256 do timestamp + election_id)
-- 3. Define sealed_at com timestamp atual
-- 4. Mantém o sealed_by definido pela aplicação
-- 5. Evita atualizações desnecessárias se já estiver lacrada
--
-- SEGURANÇA:
-- - Apenas permite lacrar (não des-lacrar)
-- - Gera hash único baseado em timestamp + ID
-- - Preserva dados existentes de lacre
-- ===============================================================================

DELIMITER $$

-- Remove trigger existente se houver
DROP TRIGGER IF EXISTS trg_election_seal_update$$

-- Cria o trigger para atualização de lacre
CREATE TRIGGER trg_election_seal_update
    BEFORE UPDATE ON elections
    FOR EACH ROW
BEGIN
    -- Declara variáveis para controle
    DECLARE seal_timestamp DATETIME(6);
    DECLARE hash_input VARCHAR(500);
    
    -- Verifica se is_sealed está mudando de FALSE para TRUE
    IF OLD.is_sealed = FALSE AND NEW.is_sealed = TRUE THEN
        
        -- Define o timestamp de lacre
        SET seal_timestamp = NOW(6);
        
        -- Prepara string para hash (ID + timestamp + título + status)
        SET hash_input = CONCAT(
            NEW.id, 
            '|', 
            DATE_FORMAT(seal_timestamp, '%Y-%m-%d %H:%i:%s.%f'),
            '|',
            NEW.title,
            '|',
            NEW.status,
            '|',
            'ELECTION_SEAL'
        );
        
        -- Atualiza automaticamente as colunas de lacre
        SET NEW.sealed_at = seal_timestamp;
        SET NEW.seal_hash = UPPER(SHA2(hash_input, 256));
        
        -- sealed_by deve ser definido pela aplicação, mas garantimos que não seja NULL
        IF NEW.sealed_by IS NULL THEN
            SET NEW.sealed_by = NEW.updated_by; -- Usa o usuário que está fazendo a atualização
        END IF;
        
        -- Log para debug (será visível nos logs do MySQL se habilitado)
        -- SET @debug_msg = CONCAT('Election ', NEW.id, ' sealed with hash: ', NEW.seal_hash);
        
    END IF;
    
    -- Segurança: Impede des-lacrar uma eleição já lacrada
    IF OLD.is_sealed = TRUE AND NEW.is_sealed = FALSE THEN
        SIGNAL SQLSTATE '45000' 
        SET MESSAGE_TEXT = 'Não é possível remover o lacre de uma eleição já lacrada por motivos de segurança';
    END IF;
    
    -- Impede modificação manual do hash se a eleição já estiver lacrada
    IF OLD.is_sealed = TRUE AND OLD.seal_hash IS NOT NULL AND NEW.seal_hash != OLD.seal_hash THEN
        SIGNAL SQLSTATE '45000' 
        SET MESSAGE_TEXT = 'Não é possível modificar o hash de lacre de uma eleição já lacrada';
    END IF;
    
END$$

-- Cria trigger para INSERT (caso uma eleição seja criada já lacrada)
DROP TRIGGER IF EXISTS trg_election_seal_insert$$

CREATE TRIGGER trg_election_seal_insert
    BEFORE INSERT ON elections
    FOR EACH ROW
BEGIN
    -- Declara variáveis para controle
    DECLARE seal_timestamp DATETIME(6);
    DECLARE hash_input VARCHAR(500);
    
    -- Se a eleição está sendo criada já lacrada
    IF NEW.is_sealed = TRUE THEN
        
        -- Define o timestamp de lacre
        SET seal_timestamp = NOW(6);
        
        -- Prepara string para hash
        SET hash_input = CONCAT(
            COALESCE(NEW.id, 'NEW'), -- Para INSERT, ID pode ser NULL ainda
            '|', 
            DATE_FORMAT(seal_timestamp, '%Y-%m-%d %H:%i:%s.%f'),
            '|',
            NEW.title,
            '|',
            NEW.status,
            '|',
            'ELECTION_SEAL'
        );
        
        -- Atualiza automaticamente as colunas de lacre
        SET NEW.sealed_at = seal_timestamp;
        SET NEW.seal_hash = UPPER(SHA2(hash_input, 256));
        
        -- sealed_by usa created_by se não estiver definido
        IF NEW.sealed_by IS NULL THEN
            SET NEW.sealed_by = NEW.created_by;
        END IF;
        
    END IF;
    
END$$

DELIMITER ;

-- ===============================================================================
-- FUNÇÃO AUXILIAR PARA VALIDAR LACRE
-- ===============================================================================

DELIMITER $$

-- Remove função existente se houver
DROP FUNCTION IF EXISTS fn_validate_election_seal$$

-- Cria função para validar lacre
CREATE FUNCTION fn_validate_election_seal(election_id INT)
RETURNS JSON
READS SQL DATA
DETERMINISTIC
BEGIN
    DECLARE v_is_sealed BOOLEAN DEFAULT FALSE;
    DECLARE v_seal_hash VARCHAR(128);
    DECLARE v_sealed_at DATETIME(6);
    DECLARE v_title VARCHAR(255);
    DECLARE v_status VARCHAR(20);
    DECLARE current_hash VARCHAR(128);
    DECLARE hash_input VARCHAR(500);
    DECLARE result JSON;
    
    -- Busca dados da eleição
    SELECT is_sealed, seal_hash, sealed_at, title, status
    INTO v_is_sealed, v_seal_hash, v_sealed_at, v_title, v_status
    FROM elections 
    WHERE id = election_id;
    
    -- Se a eleição não estiver lacrada
    IF v_is_sealed = FALSE OR v_seal_hash IS NULL THEN
        SET result = JSON_OBJECT(
            'valid', FALSE,
            'message', 'Eleição não está lacrada',
            'sealed', FALSE
        );
        RETURN result;
    END IF;
    
    -- Recalcula o hash para validação
    SET hash_input = CONCAT(
        election_id,
        '|', 
        DATE_FORMAT(v_sealed_at, '%Y-%m-%d %H:%i:%s.%f'),
        '|',
        v_title,
        '|',
        v_status,
        '|',
        'ELECTION_SEAL'
    );
    
    SET current_hash = UPPER(SHA2(hash_input, 256));
    
    -- Compara os hashes
    IF current_hash = v_seal_hash THEN
        SET result = JSON_OBJECT(
            'valid', TRUE,
            'message', 'Lacre válido e íntegro',
            'sealed', TRUE,
            'sealed_at', v_sealed_at,
            'seal_hash', v_seal_hash
        );
    ELSE
        SET result = JSON_OBJECT(
            'valid', FALSE,
            'message', 'Lacre inválido - dados foram modificados',
            'sealed', TRUE,
            'original_hash', v_seal_hash,
            'calculated_hash', current_hash
        );
    END IF;
    
    RETURN result;
    
END$$

DELIMITER ;

-- ===============================================================================
-- TESTES E VALIDAÇÕES
-- ===============================================================================

-- Exemplo de uso da função de validação:
-- SELECT fn_validate_election_seal(1) as validation_result;

-- Exemplo de teste do trigger:
-- UPDATE elections SET is_sealed = TRUE WHERE id = 1;

-- ===============================================================================
-- INFORMAÇÕES IMPORTANTES
-- ===============================================================================

/*
COMO FUNCIONA:

1. TRIGGER DE UPDATE (trg_election_seal_update):
   - Executa automaticamente ANTES de qualquer UPDATE na tabela elections
   - Detecta quando is_sealed muda de FALSE para TRUE
   - Gera automaticamente sealed_at (timestamp atual)
   - Calcula seal_hash usando SHA2-256 de: ID + timestamp + título + status
   - Preserva sealed_by (deve ser definido pela aplicação)
   - IMPEDE des-lacrar uma eleição (segurança)

2. TRIGGER DE INSERT (trg_election_seal_insert):
   - Executa automaticamente ANTES de qualquer INSERT na tabela elections
   - Aplica mesma lógica caso eleição seja criada já lacrada

3. FUNÇÃO DE VALIDAÇÃO (fn_validate_election_seal):
   - Permite validar se um lacre está íntegro
   - Recalcula o hash e compara com o armazenado
   - Retorna JSON com resultado da validação

SEGURANÇA IMPLEMENTADA:
- Impede des-lacrar eleições
- Impede modificação manual de hashes
- Hash inclui dados críticos da eleição
- Timestamp com precisão de microssegundos

COMPATIBILIDADE:
- MySQL 5.7+
- Usa funções nativas (SHA2, NOW, JSON_OBJECT)
- Triggers BEFORE para máxima compatibilidade

USO PELA APLICAÇÃO:
A aplicação deve apenas definir:
- is_sealed = TRUE
- sealed_by = admin_id (opcional)

O trigger automaticamente define:
- sealed_at = timestamp atual
- seal_hash = hash calculado automaticamente
*/