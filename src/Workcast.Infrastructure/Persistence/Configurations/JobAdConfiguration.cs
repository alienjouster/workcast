using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workcast.Core.Entities;

namespace Workcast.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="JobAd"/> entity.
/// Defines table name, column mappings, and all indexes per TECHSPEC sections 3.3 and 3.6.
/// </summary>
public sealed class JobAdConfiguration : IEntityTypeConfiguration<JobAd>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<JobAd> builder)
    {
        builder.ToTable("job_ads");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.JobBoardId)
            .HasColumnName("job_board_id")
            .IsRequired();

        builder.Property(a => a.ScrapeRunId)
            .HasColumnName("scrape_run_id");

        builder.Property(a => a.ExternalId)
            .HasColumnName("external_id")
            .HasMaxLength(512);

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

        builder.Property(a => a.ScrapedAt)
            .HasColumnName("scraped_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(a => a.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        // Primary dedup: JobBoardId + Url UNIQUE (TECHSPEC section 3.6)
        builder.HasIndex(a => new { a.JobBoardId, a.Url })
            .IsUnique()
            .HasDatabaseName("ix_job_ads_job_board_id_url");

        // Secondary dedup: JobBoardId + ExternalId UNIQUE WHERE ExternalId IS NOT NULL (TECHSPEC section 3.6)
        builder.HasIndex(a => new { a.JobBoardId, a.ExternalId })
            .IsUnique()
            .HasFilter("external_id IS NOT NULL")
            .HasDatabaseName("ix_job_ads_job_board_id_external_id");

        // Timeline queries (TECHSPEC section 3.6)
        builder.HasIndex(a => a.ScrapedAt)
            .IsDescending(true)
            .HasDatabaseName("ix_job_ads_scraped_at_desc");

        // Filtered listing queries (TECHSPEC section 3.6)
        builder.HasIndex(a => new { a.JobBoardId, a.IsActive })
            .HasDatabaseName("ix_job_ads_job_board_id_is_active");

        builder.HasOne(a => a.ScrapeRun)
            .WithMany(r => r.JobAds)
            .HasForeignKey(a => a.ScrapeRunId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
