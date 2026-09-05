using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.IntegrationHub.Domain.Entities;

namespace Nabadat.IntegrationHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Credential"/> to <c>credentials</c> (DB-08), with the intra-module FK to
/// <see cref="Integration"/>. <c>mechanism</c> and <c>status</c> persist as their snake_case wire values
/// via <see cref="CredentialMechanismConverter"/> / <see cref="CredentialStatusConverter"/>;
/// <c>scopes</c> is a Postgres <c>text[]</c>.
/// <para>Deliberately absent (ratified removals, <c>[PO-G13]</c> / BR-17): grant type, access-token
/// lifetime, expiry, sandbox flag, allowed-source-IPs — fixed in code, never columns. BR-16's
/// one-active-credential-per-integration invariant is the baseline's partial unique index.</para>
/// </summary>
public sealed class CredentialConfiguration : IEntityTypeConfiguration<Credential>
{
    public void Configure(EntityTypeBuilder<Credential> builder)
    {
        builder.ToTable("credentials");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.IntegrationId).HasColumnName("integration_id");
        builder.Property(c => c.Mechanism).HasColumnName("mechanism").HasConversion(new CredentialMechanismConverter());
        builder.Property(c => c.LabelOrClientName).HasColumnName("label_or_client_name");
        builder.Property(c => c.SecretHash).HasColumnName("secret_hash");
        builder.Property(c => c.Scopes).HasColumnName("scopes");
        builder.Property(c => c.Status).HasColumnName("status").HasConversion(new CredentialStatusConverter());
        builder.Property(c => c.GeneratedAt).HasColumnName("generated_at");
        builder.Property(c => c.GeneratedBy).HasColumnName("generated_by");
        builder.Property(c => c.RevokedAt).HasColumnName("revoked_at");

        builder.HasOne<Integration>()
            .WithMany()
            .HasForeignKey(c => c.IntegrationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
