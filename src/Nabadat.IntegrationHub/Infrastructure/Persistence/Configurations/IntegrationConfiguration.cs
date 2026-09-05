using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.IntegrationHub.Domain.Entities;

namespace Nabadat.IntegrationHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Integration"/> to <c>integrations</c> (DB-08). <c>scenario</c> persists as its
/// snake_case wire value via <see cref="ScenarioConverter"/>; <c>allowed_origins</c> is a Postgres
/// <c>text[]</c>, which Npgsql maps to <c>string[]</c> natively.
/// <para><c>created_by</c> holds an M-10 <c>user_id</c> — a logical cross-module reference for audit
/// attribution, never an enforced FK (Article 4.1). VR-F01's case-insensitive name uniqueness is the
/// baseline's <c>LOWER(name)</c> functional index.</para>
/// </summary>
public sealed class IntegrationConfiguration : IEntityTypeConfiguration<Integration>
{
    public void Configure(EntityTypeBuilder<Integration> builder)
    {
        builder.ToTable("integrations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.Name).HasColumnName("name");
        builder.Property(i => i.Description).HasColumnName("description");
        builder.Property(i => i.ServiceChannelId).HasColumnName("service_channel_id");
        builder.Property(i => i.Scenario).HasColumnName("scenario").HasConversion(new ScenarioConverter());
        builder.Property(i => i.Active).HasColumnName("active");
        builder.Property(i => i.AllowedOrigins).HasColumnName("allowed_origins");
        builder.Property(i => i.LinkExpiryOverrideHours).HasColumnName("link_expiry_override_hours");
        builder.Property(i => i.CreatedBy).HasColumnName("created_by");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<ServiceChannel>()
            .WithMany()
            .HasForeignKey(i => i.ServiceChannelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
