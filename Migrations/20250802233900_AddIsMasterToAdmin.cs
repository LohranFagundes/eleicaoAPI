using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectionApi.Net.Migrations
{
    /// <inheritdoc />
    public partial class AddIsMasterToAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_master",
                table: "admins",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_master",
                table: "admins");
        }
    }
}
