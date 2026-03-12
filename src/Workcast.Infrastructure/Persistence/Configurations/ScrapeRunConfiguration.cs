using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workcast.Core.Entities;
using Workcast.Core.Enums;
using Workcast.Core.Models;

namespace Workcast.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="ScrapeRun"/> entity.
/// Defines table name, JSONB errors column, enum conversions, and indexes
/// per TECHSPEC sections 3.4 and 3.6.
/// </summary>
public sealed class ScrapeRunConfiguration : IEntityTypeConfiguration<ScrapeRun>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ScrapeRun> builder)
    {
        builder.ToTable("scrape_runs");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.JobBoardId)
            .HasColumnName("job_board_id")
            .IsRequired();

        builder.Property(r => r.TriggeredBy)
            .HasColumnName("triggered_by")
            .HasMaxLength(50)
            .HasConversion(
                t => t.ToString().ToLowerInvariant(),
                t => Enum.Parse<TriggerSource>(t, true))
            .IsRequired();

        builder.Property(r => r.StartedAt)
            .HasColumnName("started_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(r => r.FinishedAt)
            .HasColumnName("finished_at")
            .HasColumnType("timestamptz");

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .HasConversion(
                s => s.ToString().ToLowerInvariant(),
                s => Enum.Parse<RunStatus>(s, true))
            .IsRequired();

        builder.Property(r => r.PagesScraped)
            .HasColumnName("pages_scraped")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(r => r.AdsFound)
            .HasColumnName("ads_found")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(r => r.AdsNew)
            .HasColumnName("ads_new")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(r => r.Errors)
            .HasColumnName("errors")
            .HasColumnType("jsonb")
            .HasConversion(
                errors => System.Text.Json.JsonSerializer.Serialize(errors, (System.Text.Json.JsonSerializerOptions?)null),
                json => System.Text.Json.JsonSerializer.Deserialize<List<ScrapeRunError>>(json, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<ScrapeRunError>())
            .Metadata.SetValueComparer(new ValueComparer<IList<ScrapeRunError>>(
                (a, b) => System.Text.Json.JsonSerializer.Serialize(a, (System.Text.Json.JsonSerializerOptions?)null) ==
                           System.Text.Json.JsonSerializer.Serialize(b, (System.Text.Json.JsonSerializerOptions?)null),
                v => v.Aggregate(0, (hash, e) => HashCode.Combine(hash, e.GetHashCode())),
                v => v.ToList()));

        // Run history per board: JobBoardId + StartedAt DESC (TECHSPEC section 3.6)
        builder.HasIndex(r => new { r.JobBoardId, r.StartedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_scrape_runs_job_board_id_started_at_desc");
    }
}
