using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Npgsql;

namespace Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;

/// <summary>
/// Integration-test <see cref="IEventLogWriter"/> that actually appends the M-01 audit event to the
/// Testcontainers <c>event_log</c> table (the production wiring is M-17's host adapter, which does not
/// exist yet — see TODO-M01-011; the module default is <c>NoOpEventLogWriter</c>). Registered by
/// <see cref="SurveyBuilderApplicationFactory"/> so tests can assert audit emission via
/// <see cref="SurveyBuilderApplicationFactory.CountEventsAsync"/> and payload queries (data-model §7).
/// </summary>
public sealed class DbEventLogWriter : IEventLogWriter
{
    private readonly string _connectionString;

    public DbEventLogWriter(IConfiguration configuration) =>
        _connectionString = configuration.GetConnectionString("TenantDb")
            ?? throw new InvalidOperationException("ConnectionStrings:TenantDb is not configured.");

    public async Task WriteAsync(SurveyAuditEvent auditEvent, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO event_log
                (event_id, event_type, actor_id, entity_type, entity_id, new_value, occurred_at_utc, correlation_id)
            VALUES (@event_id, @event_type, @actor_id, 'survey', @entity_id, @new_value::jsonb, now(), @correlation_id)
            """, connection);
        command.Parameters.AddWithValue("event_id", Guid.NewGuid());
        command.Parameters.AddWithValue("event_type", auditEvent.EventType);
        command.Parameters.AddWithValue("actor_id", auditEvent.ActorId);
        command.Parameters.AddWithValue("entity_id", auditEvent.SurveyId);
        command.Parameters.AddWithValue("new_value", JsonSerializer.Serialize(auditEvent.Payload));
        command.Parameters.AddWithValue("correlation_id", auditEvent.CorrelationId);
        await command.ExecuteNonQueryAsync(ct);
    }
}
