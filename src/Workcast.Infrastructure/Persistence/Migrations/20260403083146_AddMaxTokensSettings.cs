using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workcast.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMaxTokensSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "board_analyzer_max_tokens",
                table: "app_settings",
                type: "integer",
                nullable: false,
                defaultValue: 4096);

            migrationBuilder.AddColumn<int>(
                name: "letter_generation_max_tokens",
                table: "app_settings",
                type: "integer",
                nullable: false,
                defaultValue: 2048);

            migrationBuilder.AddColumn<int>(
                name: "resume_generation_max_tokens",
                table: "app_settings",
                type: "integer",
                nullable: false,
                defaultValue: 8192);

            migrationBuilder.AddColumn<int>(
                name: "scoring_max_tokens",
                table: "app_settings",
                type: "integer",
                nullable: false,
                defaultValue: 4096);

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "board_analyzer_max_tokens", "letter_generation_max_tokens", "resume_generation_max_tokens", "scoring_max_tokens" },
                values: new object[] { 4096, 2048, 8192, 4096 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "board_analyzer_max_tokens",
                table: "app_settings");

            migrationBuilder.DropColumn(
                name: "letter_generation_max_tokens",
                table: "app_settings");

            migrationBuilder.DropColumn(
                name: "resume_generation_max_tokens",
                table: "app_settings");

            migrationBuilder.DropColumn(
                name: "scoring_max_tokens",
                table: "app_settings");
        }
    }
}
