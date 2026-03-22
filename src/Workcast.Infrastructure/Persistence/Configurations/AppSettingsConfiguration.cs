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

        builder.Property(s => s.AiModel)
            .HasColumnName("ai_model")
            .HasMaxLength(100)
            .IsRequired();

        // Seed the single default row so the table is never empty.
        builder.HasData(AppSettings.CreateDefault());
    }
}
