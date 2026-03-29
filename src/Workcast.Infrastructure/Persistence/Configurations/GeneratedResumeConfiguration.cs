using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Workcast.Core.Entities;
using Workcast.Core.Models;

namespace Workcast.Infrastructure.Persistence.Configurations;

internal sealed class GeneratedResumeConfiguration : IEntityTypeConfiguration<GeneratedResume>
{
    public void Configure(EntityTypeBuilder<GeneratedResume> builder)
    {
        builder.ToTable("generated_resumes");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.ApplicationId)
            .HasColumnName("application_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(r => r.VersionNumber)
            .HasColumnName("version_number")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(r => r.HtmlContent)
            .HasColumnName("html_content")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(r => r.ModelUsed)
            .HasColumnName("model_used")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.GeneratedAt)
            .HasColumnName("generated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(r => r.OptimizationLevel)
            .HasColumnName("optimization_level")
            .HasMaxLength(20)
            .HasConversion(new ValueConverter<ResumeOptimizationLevel?, string?>(
                v => v.HasValue ? v.Value.ToString() : null,
                v => v != null ? Enum.Parse<ResumeOptimizationLevel>(v) : null))
            .IsRequired(false);

        builder.Property(r => r.IsManualEdit)
            .HasColumnName("is_manual_edit")
            .HasColumnType("boolean")
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasOne(r => r.Application)
            .WithMany()
            .HasForeignKey(r => r.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.ApplicationId)
            .HasDatabaseName("ix_generated_resumes_application_id");

        builder.HasIndex(r => new { r.ApplicationId, r.VersionNumber })
            .HasDatabaseName("ix_generated_resumes_application_id_version_number")
            .IsUnique();

        builder.HasIndex(r => new { r.ApplicationId, r.GeneratedAt })
            .HasDatabaseName("ix_generated_resumes_application_id_generated_at_desc")
            .IsDescending(false, true);
    }
}
