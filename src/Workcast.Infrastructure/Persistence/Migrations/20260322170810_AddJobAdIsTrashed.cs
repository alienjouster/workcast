using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workcast.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobAdIsTrashed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTrashed",
                table: "job_ads",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTrashed",
                table: "job_ads");
        }
    }
}
