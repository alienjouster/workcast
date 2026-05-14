using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workcast.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleDriveIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "google_drive_folder_id",
                table: "applications",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "google_drive_base_folder_id",
                table: "app_settings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "google_drive_base_path",
                table: "app_settings",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "jobs");

            migrationBuilder.AddColumn<string>(
                name: "google_drive_refresh_token",
                table: "app_settings",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "google_drive_base_folder_id", "google_drive_base_path", "google_drive_refresh_token" },
                values: new object[] { null, "jobs", null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "google_drive_folder_id",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "google_drive_base_folder_id",
                table: "app_settings");

            migrationBuilder.DropColumn(
                name: "google_drive_base_path",
                table: "app_settings");

            migrationBuilder.DropColumn(
                name: "google_drive_refresh_token",
                table: "app_settings");
        }
    }
}
