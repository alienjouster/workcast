using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workcast.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLetterGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_letter_generation_pending",
                table: "applications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "last_letter_generation_error",
                table: "applications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "letter_generation_model",
                table: "app_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "generated_letters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    html_content = table.Column<string>(type: "text", nullable: false),
                    model_used = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generated_letters", x => x.id);
                    table.ForeignKey(
                        name: "FK_generated_letters_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "Id",
                keyValue: 1,
                column: "letter_generation_model",
                value: "claude-sonnet-4-6");

            migrationBuilder.CreateIndex(
                name: "ix_generated_letters_application_id",
                table: "generated_letters",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_generated_letters_application_id_generated_at_desc",
                table: "generated_letters",
                columns: new[] { "application_id", "generated_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "generated_letters");

            migrationBuilder.DropColumn(
                name: "is_letter_generation_pending",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "last_letter_generation_error",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "letter_generation_model",
                table: "app_settings");
        }
    }
}
