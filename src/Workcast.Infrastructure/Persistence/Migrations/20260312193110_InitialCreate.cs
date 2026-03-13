using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workcast.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_boards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    scraper_config = table.Column<string>(type: "jsonb", nullable: true),
                    schedule_cron = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_scraped_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_boards", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scrape_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    job_board_id = table.Column<Guid>(type: "uuid", nullable: false),
                    triggered_by = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    pages_scraped = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ads_found = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ads_new = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    errors = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scrape_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_scrape_runs_job_boards_job_board_id",
                        column: x => x.job_board_id,
                        principalTable: "job_boards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_ads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    job_board_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scrape_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    external_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    company = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    location = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    salary_raw = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    posted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    scraped_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    raw_html = table.Column<string>(type: "text", nullable: false),
                    ai_confidence_score = table.Column<float>(type: "real", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_ads", x => x.id);
                    table.ForeignKey(
                        name: "FK_job_ads_job_boards_job_board_id",
                        column: x => x.job_board_id,
                        principalTable: "job_boards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_job_ads_scrape_runs_scrape_run_id",
                        column: x => x.scrape_run_id,
                        principalTable: "scrape_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_job_ads_job_board_id_external_id",
                table: "job_ads",
                columns: new[] { "job_board_id", "external_id" },
                unique: true,
                filter: "external_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_job_ads_job_board_id_is_active",
                table: "job_ads",
                columns: new[] { "job_board_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_job_ads_job_board_id_url",
                table: "job_ads",
                columns: new[] { "job_board_id", "url" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_ads_scrape_run_id",
                table: "job_ads",
                column: "scrape_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_ads_scraped_at_desc",
                table: "job_ads",
                column: "scraped_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_job_boards_status",
                table: "job_boards",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_scrape_runs_job_board_id_started_at_desc",
                table: "scrape_runs",
                columns: new[] { "job_board_id", "started_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_ads");

            migrationBuilder.DropTable(
                name: "scrape_runs");

            migrationBuilder.DropTable(
                name: "job_boards");
        }
    }
}
