using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workcast.Core.Entities;

namespace Workcast.Infrastructure.Persistence.Configurations;

internal sealed class AppSettingsConfiguration : IEntityTypeConfiguration<AppSettings>
{
    public void Configure(EntityTypeBuilder<AppSettings> builder)
    {
        builder.ToTable("app_settings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedNever(); // Fixed Id = 1, never auto-increment.

        builder.Property(s => s.BoardAnalyzerModel)
            .HasColumnName("board_analyzer_model")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.ScoringModel)
            .HasColumnName("scoring_model")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.ResumeFileName)
            .HasColumnName("resume_file_name")
            .HasMaxLength(512);

        builder.Property(s => s.ResumeContent)
            .HasColumnName("resume_content")
            .HasColumnType("bytea");

        builder.Property(s => s.ResumeContentType)
            .HasColumnName("resume_content_type")
            .HasMaxLength(100);

        builder.Property(s => s.ResumeUploadedAt)
            .HasColumnName("resume_uploaded_at")
            .HasColumnType("timestamptz");

        builder.Property(s => s.ResumeTemplateFileName)
            .HasColumnName("resume_template_file_name")
            .HasMaxLength(512);

        builder.Property(s => s.ResumeTemplateContent)
            .HasColumnName("resume_template_content")
            .HasColumnType("text");

        builder.Property(s => s.ResumeTemplateUploadedAt)
            .HasColumnName("resume_template_uploaded_at")
            .HasColumnType("timestamptz");

        builder.Property(s => s.ResumeGenerationModel)
            .HasColumnName("resume_generation_model")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.LetterGenerationModel)
            .HasColumnName("letter_generation_model")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.BoardAnalyzerMaxTokens)
            .HasColumnName("board_analyzer_max_tokens")
            .IsRequired()
            .HasDefaultValue(4096);

        builder.Property(s => s.ScoringMaxTokens)
            .HasColumnName("scoring_max_tokens")
            .IsRequired()
            .HasDefaultValue(4096);

        builder.Property(s => s.ResumeGenerationMaxTokens)
            .HasColumnName("resume_generation_max_tokens")
            .IsRequired()
            .HasDefaultValue(8192);

        builder.Property(s => s.LetterGenerationMaxTokens)
            .HasColumnName("letter_generation_max_tokens")
            .IsRequired()
            .HasDefaultValue(2048);

        builder.Property(s => s.InterviewTrainerModel)
            .HasColumnName("interview_trainer_model")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.InterviewTrainerMaxTokens)
            .HasColumnName("interview_trainer_max_tokens")
            .IsRequired()
            .HasDefaultValue(4096);

        builder.Property(s => s.InterviewAnswerEvaluationModel)
            .HasColumnName("interview_answer_evaluation_model")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.InterviewAnswerEvaluationMaxTokens)
            .HasColumnName("interview_answer_evaluation_max_tokens")
            .IsRequired()
            .HasDefaultValue(1024);

        builder.Property(s => s.GoogleDriveRefreshToken)
            .HasColumnName("google_drive_refresh_token")
            .HasColumnType("text");

        builder.Property(s => s.GoogleDriveBasePath)
            .HasColumnName("google_drive_base_path")
            .HasMaxLength(512)
            .IsRequired()
            .HasDefaultValue("jobs");

        builder.Property(s => s.GoogleDriveBaseFolderId)
            .HasColumnName("google_drive_base_folder_id")
            .HasMaxLength(256);

        // Seed the single default row so the table is never empty.
        builder.HasData(AppSettings.CreateDefault());
    }
}
