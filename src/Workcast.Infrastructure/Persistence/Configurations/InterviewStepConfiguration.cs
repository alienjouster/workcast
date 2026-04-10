using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workcast.Core.Entities;

namespace Workcast.Infrastructure.Persistence.Configurations;

internal sealed class InterviewStepConfiguration : IEntityTypeConfiguration<InterviewStep>
{
    public void Configure(EntityTypeBuilder<InterviewStep> builder)
    {
        builder.ToTable("interview_steps");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.ApplicationId)
            .HasColumnName("application_id")
            .IsRequired();

        builder.Property(s => s.StepNumber)
            .HasColumnName("step_number")
            .IsRequired();

        builder.Property(s => s.Date)
            .HasColumnName("date")
            .HasColumnType("date");

        builder.Property(s => s.Time)
            .HasColumnName("time")
            .HasColumnType("time");

        builder.Property(s => s.DurationMinutes)
            .HasColumnName("duration_minutes");

        builder.Property(s => s.Timezone)
            .HasColumnName("timezone")
            .HasMaxLength(50)
            .IsRequired()
            .HasDefaultValue("CEST");

        builder.Property(s => s.IsOnSite)
            .HasColumnName("is_on_site")
            .IsRequired();

        builder.Property(s => s.RemoteCallLink)
            .HasColumnName("remote_call_link")
            .HasMaxLength(2048);

        builder.Property(s => s.Interviewers)
            .HasColumnName("interviewers")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<InterviewStepInterviewer>>(v, (JsonSerializerOptions?)null)
                     ?? new List<InterviewStepInterviewer>());

        builder.Property(s => s.Notes)
            .HasColumnName("notes");

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(s => s.ApplicationId)
            .HasDatabaseName("ix_interview_steps_application_id");

        builder.HasOne<Application>()
            .WithMany()
            .HasForeignKey(s => s.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
