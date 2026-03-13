using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workcast.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ListingOnlyExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ai_confidence_score",
                table: "job_ads");

            migrationBuilder.DropColumn(
                name: "raw_html",
                table: "job_ads");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "ai_confidence_score",
                table: "job_ads",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<string>(
                name: "raw_html",
                table: "job_ads",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
