using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectionApi.Net.Migrations
{
    /// <inheritdoc />
    public partial class AddHybridPhotoStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "photo_data",
                table: "candidates",
                type: "longblob",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "photo_file_name",
                table: "candidates",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "photo_mime_type",
                table: "candidates",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "photo_data",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "photo_file_name",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "photo_mime_type",
                table: "candidates");
        }
    }
}
