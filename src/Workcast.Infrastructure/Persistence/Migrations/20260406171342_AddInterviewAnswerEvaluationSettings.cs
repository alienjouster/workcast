using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workcast.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewAnswerEvaluationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "interview_answer_evaluation_model",
                table: "app_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "claude-sonnet-4-5");

            migrationBuilder.AddColumn<int>(
                name: "interview_answer_evaluation_max_tokens",
                table: "app_settings",
                type: "integer",
                nullable: false,
                defaultValue: 1024);

            migrationBuilder.UpdateData(
                table: "app_settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "interview_answer_evaluation_model", "interview_answer_evaluation_max_tokens" },
                values: new object[] { "claude-sonnet-4-5", 1024 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "interview_answer_evaluation_model",
                table: "app_settings");

            migrationBuilder.DropColumn(
                name: "interview_answer_evaluation_max_tokens",
                table: "app_settings");
        }
    }
}
