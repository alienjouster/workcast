using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workcast.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitAiModelPerOperation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "board_analyzer_model",
                table: "app_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "claude-sonnet-4-5");

            migrationBuilder.AddColumn<string>(
                name: "scoring_model",
                table: "app_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "claude-haiku-4-5-20251001");

            migrationBuilder.DropColumn(
                name: "ai_model",
                table: "app_settings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ai_model",
                table: "app_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "claude-sonnet-4-5");

            migrationBuilder.DropColumn(
                name: "board_analyzer_model",
                table: "app_settings");

            migrationBuilder.DropColumn(
                name: "scoring_model",
                table: "app_settings");
        }
    }
}
