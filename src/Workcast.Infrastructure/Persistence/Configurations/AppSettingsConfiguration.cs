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

        // Seed the single default row so the table is never empty.
        builder.HasData(AppSettings.CreateDefault());
    }
}
