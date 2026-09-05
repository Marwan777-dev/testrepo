using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="TemplateSnapshot"/> to the tenant-schema <c>template_snapshots</c> table (DB-08,
/// data-model.md §2.9). Keyed by <c>template_id</c> (1:1 with <c>templates</c>, ON DELETE CASCADE in
/// the DDL). <see cref="TemplateSnapshot.Snapshot"/> is the raw jsonb payload (the serialised
/// <c>SurveySnapshot</c>). This replaces the interim <c>HasKey(t =&gt; t.TemplateId)</c> that lived in
/// <c>TenantDbContext.OnModelCreating</c> while the entity was a skeleton (TODO-M01-007).
/// </summary>
public sealed class TemplateSnapshotConfiguration : IEntityTypeConfiguration<TemplateSnapshot>
{
    public void Configure(EntityTypeBuilder<TemplateSnapshot> builder)
    {
        builder.ToTable("template_snapshots");

        builder.HasKey(t => t.TemplateId);

        builder.Property(t => t.TemplateId).HasColumnName("template_id").ValueGeneratedNever();
        builder.Property(t => t.Snapshot).HasColumnName("snapshot").HasColumnType("jsonb");
        builder.Property(t => t.SchemaVersion).HasColumnName("schema_version");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");

        // 1:1 with templates (template_id is both PK and FK, ON DELETE CASCADE in the DDL). Modelling
        // the relationship lets EF order the inserts (template before its snapshot) so a save-as-template
        // that adds both in one SaveChanges does not trip the FK.
        builder.HasOne<Template>()
            .WithOne()
            .HasForeignKey<TemplateSnapshot>(t => t.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
