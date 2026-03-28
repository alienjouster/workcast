using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workcast.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHangfireJobIdAndRunStatusSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Stores the Hangfire job ID so ScrapeRunStateFilter can correlate state-change
            // events with the correct ScrapeRun record. Nullable: runs created before this
            // migration do not have a Hangfire job ID.
            migrationBuilder.AddColumn<string>(
                name: "hangfire_job_id",
                table: "scrape_runs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_scrape_runs_hangfire_job_id",
                table: "scrape_runs",
                column: "hangfire_job_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_scrape_runs_hangfire_job_id",
                table: "scrape_runs");

            migrationBuilder.DropColumn(
                name: "hangfire_job_id",
                table: "scrape_runs");
        }
    }
}
