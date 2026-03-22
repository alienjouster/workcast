using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workcast.Core.Entities;

namespace Workcast.Infrastructure.Persistence.Configurations;

internal sealed class AdScoringConfiguration : IEntityTypeConfiguration<AdScoring>
{
    public void Configure(EntityTypeBuilder<AdScoring> builder)
    {
        builder.ToTable("ad_scorings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.JobAdId)
            .HasColumnName("job_ad_id")
            .IsRequired();

        builder.Property(s => s.ScoredAt)
            .HasColumnName("scored_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(s => s.OverallScore)
            .HasColumnName("overall_score")
            .IsRequired();

        builder.Property(s => s.Summary)
            .HasColumnName("summary")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(s => s.Requirements)
            .HasColumnName("requirements")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<ScoringRequirement>>(v, (JsonSerializerOptions?)null) ?? new List<ScoringRequirement>());

        // One scoring result per job ad.
        builder.HasIndex(s => s.JobAdId)
            .IsUnique()
            .HasDatabaseName("ix_ad_scorings_job_ad_id");

        builder.HasOne(s => s.JobAd)
            .WithMany()
            .HasForeignKey(s => s.JobAdId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
