using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectionApi.Net.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoFactorToAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "require_two_factor_auth",
                table: "admins",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "two_factor_token",
                table: "admins",
                type: "varchar(6)",
                maxLength: 6,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "two_factor_token_expiry",
                table: "admins",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "two_factor_verified_at",
                table: "admins",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "require_two_factor_auth",
                table: "admins");

            migrationBuilder.DropColumn(
                name: "two_factor_token",
                table: "admins");

            migrationBuilder.DropColumn(
                name: "two_factor_token_expiry",
                table: "admins");

            migrationBuilder.DropColumn(
                name: "two_factor_verified_at",
                table: "admins");
        }
    }
}
