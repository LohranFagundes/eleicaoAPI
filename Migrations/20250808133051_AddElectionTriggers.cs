using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectionApi.Net.Migrations
{
    /// <inheritdoc />
    public partial class AddElectionTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Trigger para atualizar company_id automaticamente ao criar eleição
            // Este trigger verifica se existe uma company ativa e a vincula à eleição
            migrationBuilder.Sql(@"
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
            ");

            // Trigger para atualizar campos de seal automaticamente
            // Este trigger monitora alterações nos campos is_sealed e busca informações da system_seals
            migrationBuilder.Sql(@"
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
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove trigger de atribuição automática de company
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_election_auto_assign_company");
            
            // Remove trigger de atualização automática de seal
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_election_auto_update_seal");
        }
    }
}
