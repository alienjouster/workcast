using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workcast.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdScoringAndResume : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add resume storage columns to the global settings singleton row.
            migrationBuilder.AddColumn<byte[]>(
                name: "resume_content",
                table: "app_settings",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resume_content_type",
                table: "app_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resume_file_name",
                table: "app_settings",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "resume_uploaded_at",
                table: "app_settings",
                type: "timestamptz",
                nullable: true);

            // Create the ad_scorings table (one scoring result per job ad).
            migrationBuilder.CreateTable(
                name: "ad_scorings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    job_ad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scored_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    overall_score = table.Column<double>(type: "double precision", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    requirements = table.Column<string>(type: "jsonb", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_scorings", x => x.id);
                    table.ForeignKey(
                        name: "FK_ad_scorings_job_ads_job_ad_id",
                        column: x => x.job_ad_id,
                        principalTable: "job_ads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ad_scorings_job_ad_id",
                table: "ad_scorings",
                column: "job_ad_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ad_scorings");

            migrationBuilder.DropColumn(name: "resume_content",      table: "app_settings");
            migrationBuilder.DropColumn(name: "resume_content_type", table: "app_settings");
            migrationBuilder.DropColumn(name: "resume_file_name",    table: "app_settings");
            migrationBuilder.DropColumn(name: "resume_uploaded_at",  table: "app_settings");
        }
    }
}
