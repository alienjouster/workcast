using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Workcast.Core.Entities;

namespace Workcast.Infrastructure.Persistence.Configurations;

internal sealed class GeneratedLetterConfiguration : IEntityTypeConfiguration<GeneratedLetter>
{
    public void Configure(EntityTypeBuilder<GeneratedLetter> builder)
    {
        builder.ToTable("generated_letters");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(l => l.ApplicationId)
            .HasColumnName("application_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(l => l.HtmlContent)
            .HasColumnName("html_content")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(l => l.ModelUsed)
            .HasColumnName("model_used")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.GeneratedAt)
            .HasColumnName("generated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasOne(l => l.Application)
            .WithMany()
            .HasForeignKey(l => l.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => l.ApplicationId)
            .HasDatabaseName("ix_generated_letters_application_id");

        builder.HasIndex(l => new { l.ApplicationId, l.GeneratedAt })
            .HasDatabaseName("ix_generated_letters_application_id_generated_at_desc")
            .IsDescending(false, true);
    }
}
