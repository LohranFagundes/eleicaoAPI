using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectionApi.Net.Migrations
{
    /// <inheritdoc />
    public partial class MakeCompanyIdRequiredAndUpdateSealTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remover triggers antigos se existirem
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_election_auto_assign_company");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_election_auto_update_seal");

            // Tornar company_id obrigatório
            migrationBuilder.AlterColumn<int>(
                name: "company_id",
                table: "elections",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // Criar trigger atualizado para seal que usa as colunas corretas
            migrationBuilder.Sql(@"
                CREATE TRIGGER tr_election_seal_update
                BEFORE UPDATE ON elections
                FOR EACH ROW
                BEGIN
                    -- Detecta se o seal_hash foi definido (mudança de NULL para valor)
                    IF OLD.seal_hash IS NULL AND NEW.seal_hash IS NOT NULL THEN
                        SET NEW.is_sealed = 1;
                        SET NEW.sealed_at = NOW();
                        -- sealed_by deve ser definido pela aplicação
                    END IF;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remover trigger criado
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_election_seal_update");

            // Tornar company_id nullable novamente
            migrationBuilder.AlterColumn<int>(
                name: "company_id",
                table: "elections",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
