using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workcast.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLetterVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_generated_letters_application_id_generated_at_desc",
                table: "generated_letters");

            migrationBuilder.AddColumn<bool>(
                name: "is_manual_edit",
                table: "generated_letters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "version_number",
                table: "generated_letters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill sequential version numbers per application, ordered by generated_at ASC,
            // so existing rows get 1, 2, 3, … rather than all defaulting to 0 (which would
            // violate the unique index below if an application already had multiple letters).
            migrationBuilder.Sql(@"
                UPDATE generated_letters gl
                SET    version_number = seq.rn
                FROM   (
                           SELECT id,
                                  ROW_NUMBER() OVER (
                                      PARTITION BY application_id
                                      ORDER BY generated_at ASC
                                  ) AS rn
                           FROM   generated_letters
                       ) seq
                WHERE  gl.id = seq.id;
            ");

            migrationBuilder.CreateIndex(
                name: "ix_generated_letters_application_id_version_number",
                table: "generated_letters",
                columns: new[] { "application_id", "version_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_generated_letters_application_id_version_number",
                table: "generated_letters");

            migrationBuilder.DropColumn(
                name: "is_manual_edit",
                table: "generated_letters");

            migrationBuilder.DropColumn(
                name: "version_number",
                table: "generated_letters");

            migrationBuilder.CreateIndex(
                name: "ix_generated_letters_application_id_generated_at_desc",
                table: "generated_letters",
                columns: new[] { "application_id", "generated_at" },
                descending: new[] { false, true });
        }
    }
}
