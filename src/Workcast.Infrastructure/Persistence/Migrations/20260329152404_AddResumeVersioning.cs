using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workcast.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResumeVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_manual_edit",
                table: "generated_resumes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "optimization_level",
                table: "generated_resumes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version_number",
                table: "generated_resumes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill sequential version numbers per application, ordered by generated_at ASC,
            // so existing rows get 1, 2, 3, … rather than all defaulting to 1 (which would
            // violate the unique index below if an application already had multiple resumes).
            migrationBuilder.Sql(@"
                UPDATE generated_resumes gr
                SET    version_number = seq.rn
                FROM   (
                           SELECT id,
                                  ROW_NUMBER() OVER (
                                      PARTITION BY application_id
                                      ORDER BY generated_at ASC
                                  ) AS rn
                           FROM   generated_resumes
                       ) seq
                WHERE  gr.id = seq.id;
            ");

            migrationBuilder.CreateIndex(
                name: "ix_generated_resumes_application_id_version_number",
                table: "generated_resumes",
                columns: new[] { "application_id", "version_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_generated_resumes_application_id_version_number",
                table: "generated_resumes");

            migrationBuilder.DropColumn(
                name: "is_manual_edit",
                table: "generated_resumes");

            migrationBuilder.DropColumn(
                name: "optimization_level",
                table: "generated_resumes");

            migrationBuilder.DropColumn(
                name: "version_number",
                table: "generated_resumes");
        }
    }
}
