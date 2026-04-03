using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workcast.Core.Entities;

namespace Workcast.Infrastructure.Persistence.Configurations;

internal sealed class InterviewDrillPlanConfiguration : IEntityTypeConfiguration<InterviewDrillPlan>
{
    public void Configure(EntityTypeBuilder<InterviewDrillPlan> builder)
    {
        builder.ToTable("interview_drill_plans");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.ApplicationId)
            .HasColumnName("application_id")
            .IsRequired();

        builder.Property(p => p.GeneratedAt)
            .HasColumnName("generated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(p => p.ModelUsed)
            .HasColumnName("model_used")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Questions)
            .HasColumnName("questions")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<InterviewQuestion>>(v, (JsonSerializerOptions?)null) ?? new List<InterviewQuestion>());

        // One plan per application — regenerating replaces the existing one.
        builder.HasIndex(p => p.ApplicationId)
            .IsUnique()
            .HasDatabaseName("ix_interview_drill_plans_application_id");

        builder.HasOne<Application>()
            .WithMany()
            .HasForeignKey(p => p.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
