using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workcast.Core.Entities;

namespace Workcast.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> builder)
    {
        builder.ToTable("applications");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.JobAdId)
            .HasColumnName("job_ad_id");
        // No FK relationship — application outlives the source job ad.

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(a => a.IsTrashed)
            .HasColumnName("is_trashed")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(a => a.Url)
            .HasColumnName("url")
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(a => a.Title)
            .HasColumnName("title")
            .HasMaxLength(512);

        builder.Property(a => a.Company)
            .HasColumnName("company")
            .HasMaxLength(255);

        builder.Property(a => a.Location)
            .HasColumnName("location")
            .HasMaxLength(255);

        builder.Property(a => a.SalaryRaw)
            .HasColumnName("salary_raw")
            .HasMaxLength(255);

        builder.Property(a => a.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(a => a.PostedAt)
            .HasColumnName("posted_at")
            .HasColumnType("timestamptz");

        builder.Property(a => a.ExternalId)
            .HasColumnName("external_id")
            .HasMaxLength(512);

        builder.Property(a => a.OverallScore)
            .HasColumnName("overall_score");

        builder.Property(a => a.ScoredAt)
            .HasColumnName("scored_at")
            .HasColumnType("timestamptz");

        builder.Property(a => a.Summary)
            .HasColumnName("summary")
            .HasColumnType("text");

        builder.Property(a => a.Recommendation)
            .HasColumnName("recommendation")
            .HasColumnType("text");

        builder.Property(a => a.JobAdContent)
            .HasColumnName("job_ad_content")
            .HasColumnType("text");

        builder.Property(a => a.Requirements)
            .HasColumnName("requirements")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<ScoringRequirement>>(v, (JsonSerializerOptions?)null) ?? new List<ScoringRequirement>());

        builder.HasIndex(a => a.CreatedAt)
            .IsDescending()
            .HasDatabaseName("ix_applications_created_at_desc");

        builder.HasIndex(a => a.JobAdId)
            .HasDatabaseName("ix_applications_job_ad_id");

        builder.HasIndex(a => a.IsTrashed)
            .HasDatabaseName("ix_applications_is_trashed");
    }
}
