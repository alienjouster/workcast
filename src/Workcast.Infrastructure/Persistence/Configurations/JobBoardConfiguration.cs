using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workcast.Core.Entities;
using Workcast.Core.Enums;
using Workcast.Core.Models;

namespace Workcast.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="JobBoard"/> entity.
/// Defines table name, column mappings, JSONB column, and indexes per TECHSPEC section 3.2 and 3.6.
/// </summary>
public sealed class JobBoardConfiguration : IEntityTypeConfiguration<JobBoard>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<JobBoard> builder)
    {
        builder.ToTable("job_boards");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(b => b.Name)
            .HasColumnName("name")
            .HasMaxLength(255);

        builder.Property(b => b.Url)
            .HasColumnName("url")
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(b => b.ScraperConfig)
            .HasColumnName("scraper_config")
            .HasColumnType("jsonb")
            .HasConversion(
                config => config == null ? null : System.Text.Json.JsonSerializer.Serialize(config, (System.Text.Json.JsonSerializerOptions?)null),
                json => json == null ? null : System.Text.Json.JsonSerializer.Deserialize<ScraperConfig>(json, (System.Text.Json.JsonSerializerOptions?)null));

        builder.Property(b => b.ScheduleCron)
            .HasColumnName("schedule_cron")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(b => b.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .HasConversion(
                s => s.ToString().ToLowerInvariant(),
                s => Enum.Parse<BoardStatus>(s, true))
            .IsRequired();

        builder.Property(b => b.LastScrapedAt)
            .HasColumnName("last_scraped_at")
            .HasColumnType("timestamptz");

        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(b => b.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Index: filtering active/paused boards (TECHSPEC section 3.6)
        builder.HasIndex(b => b.Status)
            .HasDatabaseName("ix_job_boards_status");

        builder.HasMany(b => b.ScrapeRuns)
            .WithOne(r => r.JobBoard)
            .HasForeignKey(r => r.JobBoardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.JobAds)
            .WithOne(a => a.JobBoard)
            .HasForeignKey(a => a.JobBoardId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
