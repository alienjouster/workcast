using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workcast.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewDrillPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_interview_drill_pending",
                table: "applications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "last_interview_drill_error",
                table: "applications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "interview_trainer_max_tokens",
                table: "app_settings",
                type: "integer",
                nullable: false,
                defaultValue: 4096);

            migrationBuilder.AddColumn<string>(
                name: "interview_trainer_model",
                table: "app_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "interview_drill_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    model_used = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    questions = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interview_drill_plans", x => x.id);
                    table.ForeignKey(
                        name: "FK_interview_drill_plans_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "interview_trainer_max_tokens", "interview_trainer_model" },
                values: new object[] { 4096, "claude-sonnet-4-5" });

            migrationBuilder.CreateIndex(
                name: "ix_interview_drill_plans_application_id",
                table: "interview_drill_plans",
                column: "application_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "interview_drill_plans");

            migrationBuilder.DropColumn(
                name: "is_interview_drill_pending",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "last_interview_drill_error",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "interview_trainer_max_tokens",
                table: "app_settings");

            migrationBuilder.DropColumn(
                name: "interview_trainer_model",
                table: "app_settings");
        }
    }
}
