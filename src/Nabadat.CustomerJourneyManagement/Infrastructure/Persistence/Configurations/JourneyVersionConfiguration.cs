using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.CustomerJourneyManagement.Domain.Entities;

namespace Nabadat.CustomerJourneyManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="JourneyVersion"/> to the tenant-schema <c>journey_versions</c> table (DB-08,
/// explicit columns). <see cref="JourneyVersion.SnapshotPayload"/> is an opaque <c>jsonb</c>
/// payload; rows are written once and never updated.
/// </summary>
public sealed class JourneyVersionConfiguration : IEntityTypeConfiguration<JourneyVersion>
{
    public void Configure(EntityTypeBuilder<JourneyVersion> builder)
    {
        builder.ToTable("journey_versions");

        builder.HasKey(v => v.VersionId);

        builder.Property(v => v.VersionId).HasColumnName("version_id").ValueGeneratedNever();
        builder.Property(v => v.JourneyId).HasColumnName("journey_id");
        builder.Property(v => v.VersionNumber).HasColumnName("version_number");
        builder.Property(v => v.PublishedBy).HasColumnName("published_by");
        builder.Property(v => v.PublishedAt).HasColumnName("published_at");
        builder.Property(v => v.SnapshotPayload).HasColumnName("snapshot_payload").HasColumnType("jsonb");
    }
}
