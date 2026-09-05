using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nabadat.IntegrationHub.Domain.Entities;

namespace Nabadat.IntegrationHub.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="IntegrationRequestLog"/> to the DB-04 monthly-partitioned
/// <c>integration_request_logs</c> table (DB-08).
///
/// <para>Two mappings follow directly from partitioning: the key is the composite
/// <c>(id, timestamp)</c> — a partitioned table's key must include its partition column — and the FK to
/// <see cref="Integration"/> stays <b>nullable</b> with <c>Restrict</c>, because an auth-rejected request
/// may fail before the integration is resolved yet must still be logged.</para>
///
/// <para><c>parameters_received</c> / <c>response_returned</c> are <c>jsonb</c> held as serialized
/// strings; <c>scenario</c> and <c>result_code</c> are plain text (the wire value exactly as returned,
/// not a converted enum — see <c>IntegrationRequestLog.ResultCode</c>). The table is append-only: EF is
/// only ever used to insert and read here.</para>
/// </summary>
public sealed class IntegrationRequestLogConfiguration : IEntityTypeConfiguration<IntegrationRequestLog>
{
    public void Configure(EntityTypeBuilder<IntegrationRequestLog> builder)
    {
        builder.ToTable("integration_request_logs");

        builder.HasKey(l => new { l.Id, l.Timestamp });

        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.IntegrationId).HasColumnName("integration_id");
        builder.Property(l => l.Timestamp).HasColumnName("timestamp");
        builder.Property(l => l.Method).HasColumnName("method");
        builder.Property(l => l.Path).HasColumnName("path");
        builder.Property(l => l.Scenario).HasColumnName("scenario");
        builder.Property(l => l.ParametersReceived).HasColumnName("parameters_received").HasColumnType("jsonb");
        builder.Property(l => l.ResponseReturned).HasColumnName("response_returned").HasColumnType("jsonb");
        builder.Property(l => l.HttpStatus).HasColumnName("http_status");
        builder.Property(l => l.ResultCode).HasColumnName("result_code");
        builder.Property(l => l.LatencyMs).HasColumnName("latency_ms");
        builder.Property(l => l.CredentialLabel).HasColumnName("credential_label");
        builder.Property(l => l.RejectionStage).HasColumnName("rejection_stage");

        builder.HasOne<Integration>()
            .WithMany()
            .HasForeignKey(l => l.IntegrationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
