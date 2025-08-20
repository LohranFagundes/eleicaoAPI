using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectionApi.Net.Migrations
{
    /// <inheritdoc />
    public partial class CompleteElectionSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ip_address",
                table: "votes",
                type: "varchar(45)",
                maxLength: 45,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "is_blank_vote",
                table: "votes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_null_vote",
                table: "votes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "user_agent",
                table: "votes",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "positions",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "is_sealed",
                table: "elections",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "seal_hash",
                table: "elections",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "sealed_at",
                table: "elections",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sealed_by",
                table: "elections",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "party",
                table: "candidates",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "secure_votes",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    vote_id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    vote_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    encrypted_vote_data = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    vote_hash = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    vote_signature = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    vote_weight = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    voted_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ip_address = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    user_agent = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    device_fingerprint = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    voter_id = table.Column<int>(type: "int", nullable: false),
                    election_id = table.Column<int>(type: "int", nullable: false),
                    position_id = table.Column<int>(type: "int", nullable: false),
                    is_blank_vote = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_null_vote = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    encrypted_justification = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    creation_hash = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    is_valid = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    election_seal_hash = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_secure_votes", x => x.id);
                    table.ForeignKey(
                        name: "FK_secure_votes_elections_election_id",
                        column: x => x.election_id,
                        principalTable: "elections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_secure_votes_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_secure_votes_voters_voter_id",
                        column: x => x.voter_id,
                        principalTable: "voters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "system_seals",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    seal_hash = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    seal_type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    election_id = table.Column<int>(type: "int", nullable: false),
                    sealed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    sealed_by = table.Column<int>(type: "int", nullable: false),
                    system_data = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ip_address = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    user_agent = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_valid = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_seals", x => x.id);
                    table.ForeignKey(
                        name: "FK_system_seals_admins_sealed_by",
                        column: x => x.sealed_by,
                        principalTable: "admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_system_seals_elections_election_id",
                        column: x => x.election_id,
                        principalTable: "elections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "vote_receipts",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    voter_id = table.Column<int>(type: "int", nullable: false),
                    election_id = table.Column<int>(type: "int", nullable: false),
                    receipt_token = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    vote_hash = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    voted_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ip_address = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    user_agent = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    vote_data = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_valid = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vote_receipts", x => x.id);
                    table.ForeignKey(
                        name: "FK_vote_receipts_elections_election_id",
                        column: x => x.election_id,
                        principalTable: "elections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vote_receipts_voters_voter_id",
                        column: x => x.voter_id,
                        principalTable: "voters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "zero_reports",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    election_id = table.Column<int>(type: "int", nullable: false),
                    generated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    generated_by = table.Column<int>(type: "int", nullable: false),
                    report_data = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    report_hash = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    total_registered_voters = table.Column<int>(type: "int", nullable: false),
                    total_candidates = table.Column<int>(type: "int", nullable: false),
                    total_positions = table.Column<int>(type: "int", nullable: false),
                    total_votes = table.Column<int>(type: "int", nullable: false),
                    ip_address = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zero_reports", x => x.id);
                    table.ForeignKey(
                        name: "FK_zero_reports_admins_generated_by",
                        column: x => x.generated_by,
                        principalTable: "admins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_zero_reports_elections_election_id",
                        column: x => x.election_id,
                        principalTable: "elections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_secure_votes_election_id",
                table: "secure_votes",
                column: "election_id");

            migrationBuilder.CreateIndex(
                name: "IX_secure_votes_position_id",
                table: "secure_votes",
                column: "position_id");

            migrationBuilder.CreateIndex(
                name: "IX_secure_votes_vote_id",
                table: "secure_votes",
                column: "vote_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_secure_votes_voted_at",
                table: "secure_votes",
                column: "voted_at");

            migrationBuilder.CreateIndex(
                name: "IX_secure_votes_voter_id_election_id",
                table: "secure_votes",
                columns: new[] { "voter_id", "election_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_system_seals_election_id_seal_type",
                table: "system_seals",
                columns: new[] { "election_id", "seal_type" });

            migrationBuilder.CreateIndex(
                name: "IX_system_seals_sealed_by",
                table: "system_seals",
                column: "sealed_by");

            migrationBuilder.CreateIndex(
                name: "IX_vote_receipts_election_id",
                table: "vote_receipts",
                column: "election_id");

            migrationBuilder.CreateIndex(
                name: "IX_vote_receipts_receipt_token",
                table: "vote_receipts",
                column: "receipt_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vote_receipts_voter_id_election_id",
                table: "vote_receipts",
                columns: new[] { "voter_id", "election_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_zero_reports_election_id",
                table: "zero_reports",
                column: "election_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_zero_reports_generated_by",
                table: "zero_reports",
                column: "generated_by");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "secure_votes");

            migrationBuilder.DropTable(
                name: "system_seals");

            migrationBuilder.DropTable(
                name: "vote_receipts");

            migrationBuilder.DropTable(
                name: "zero_reports");

            migrationBuilder.DropColumn(
                name: "ip_address",
                table: "votes");

            migrationBuilder.DropColumn(
                name: "is_blank_vote",
                table: "votes");

            migrationBuilder.DropColumn(
                name: "is_null_vote",
                table: "votes");

            migrationBuilder.DropColumn(
                name: "user_agent",
                table: "votes");

            migrationBuilder.DropColumn(
                name: "name",
                table: "positions");

            migrationBuilder.DropColumn(
                name: "is_sealed",
                table: "elections");

            migrationBuilder.DropColumn(
                name: "seal_hash",
                table: "elections");

            migrationBuilder.DropColumn(
                name: "sealed_at",
                table: "elections");

            migrationBuilder.DropColumn(
                name: "sealed_by",
                table: "elections");

            migrationBuilder.DropColumn(
                name: "party",
                table: "candidates");
        }
    }
}
