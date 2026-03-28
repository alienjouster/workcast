using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workcast.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResumeTemplateAndGeneratedResume : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "resume_generation_model",
                table: "app_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "resume_template_content",
                table: "app_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resume_template_file_name",
                table: "app_settings",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "resume_template_uploaded_at",
                table: "app_settings",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "generated_resumes",
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
                    table.PrimaryKey("PK_generated_resumes", x => x.id);
                    table.ForeignKey(
                        name: "FK_generated_resumes_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "resume_generation_model", "resume_template_content", "resume_template_file_name", "resume_template_uploaded_at" },
                values: new object[] { "claude-sonnet-4-6", null, null, null });

            migrationBuilder.CreateIndex(
                name: "ix_generated_resumes_application_id",
                table: "generated_resumes",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_generated_resumes_application_id_generated_at_desc",
                table: "generated_resumes",
                columns: new[] { "application_id", "generated_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "generated_resumes");

            migrationBuilder.DropColumn(
                name: "resume_generation_model",
                table: "app_settings");

            migrationBuilder.DropColumn(
                name: "resume_template_content",
                table: "app_settings");

            migrationBuilder.DropColumn(
                name: "resume_template_file_name",
                table: "app_settings");

            migrationBuilder.DropColumn(
                name: "resume_template_uploaded_at",
                table: "app_settings");
        }
    }
}
