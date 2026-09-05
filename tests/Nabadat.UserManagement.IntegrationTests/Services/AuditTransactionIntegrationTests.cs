using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.IntegrationTests.Infrastructure;
using Npgsql;
using Xunit;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;

namespace Nabadat.UserManagement.IntegrationTests.Services;

/// <summary>
/// Service-level integration tests for FR-015's atomic, append-only audit guarantee,
/// driven against the real <see cref="ITenantDbContext"/> + <see cref="IUserManagementEventPublisher"/>
/// over the Testcontainers Postgres (skipping the HTTP layer to control the transaction
/// boundary directly):
/// <list type="bullet">
///   <item>aborting the unit of work after the entity write but before commit rolls back
///   <i>both</i> the entity change and the audit event;</item>
///   <item>successive mutations <i>append</i> distinct event rows — a prior row is never
///   overwritten (append-only).</item>
/// </list>
/// </summary>
[Collection(UserManagementIntegrationCollection.Name)]
public sealed class AuditTransactionIntegrationTests
{
    private readonly UserManagementApplicationFactory _factory;

    public AuditTransactionIntegrationTests(UserManagementApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Aborting_the_unit_of_work_rolls_back_both_the_entity_change_and_the_event()
    {
        var user = await _factory.SeedEnrolledUserAsync(persona: "P-01");

        using (var scope = _factory.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var uow = sp.GetRequiredService<ITenantDbContext>();
            var users = sp.GetRequiredService<ITenantUserService>();
            var events = sp.GetRequiredService<IUserManagementEventPublisher>();
            var clock = sp.GetRequiredService<TimeProvider>();

            var loaded = await users.GetByIdAsync(user.UserId);
            loaded!.Persona = "P-02";

            // Stage the entity write + the event, then abort before commit. The whole unit
            // of work must roll back — neither write may survive (FR-015).
            var act = async () => await uow.ExecuteAsync(async () =>
            {
                await users.UpdateAsync(loaded);
                await events.PublishAsync(Event(user.UserId, "user.updated", Guid.NewGuid(), clock));
                throw new InvalidOperationException("simulated abort before commit");
            });

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        // Entity change rolled back: the persona is still its seeded value.
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<ITenantUserService>();
            var reread = await users.GetByIdAsync(user.UserId);
            reread!.Persona.Should().Be("P-01");
        }

        // Event write rolled back: no user.updated event landed for this entity.
        (await _factory.CountEventsByEntityAsync(user.UserId, "user.updated")).Should().Be(0);
    }

    [Fact]
    public async Task Audit_events_are_append_only_a_second_mutation_does_not_overwrite_the_first()
    {
        // entity_id carries no FK, so a synthetic id isolates this row-shape test.
        var entityId = Guid.NewGuid();
        var marker1 = Guid.NewGuid();
        var marker2 = Guid.NewGuid();

        await PublishAsync(entityId, "permission.modified", marker1);
        (await _factory.CountEventsByEntityAsync(entityId, "permission.modified")).Should().Be(1);

        await PublishAsync(entityId, "permission.modified", marker2);

        // A second mutation appends a new row rather than updating the existing one:
        // the count grows and BOTH markers survive (the first row was not overwritten).
        (await _factory.CountEventsByEntityAsync(entityId, "permission.modified")).Should().Be(2);
        (await CountWithMarkerAsync(entityId, marker1)).Should().Be(1);
        (await CountWithMarkerAsync(entityId, marker2)).Should().Be(1);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task PublishAsync(Guid entityId, string eventType, Guid marker)
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var uow = sp.GetRequiredService<ITenantDbContext>();
        var events = sp.GetRequiredService<IUserManagementEventPublisher>();
        var clock = sp.GetRequiredService<TimeProvider>();
        await uow.ExecuteAsync(() => events.PublishAsync(Event(entityId, eventType, marker, clock)));
    }

    private static UserManagementEvent Event(Guid entityId, string eventType, Guid marker, TimeProvider clock) => new()
    {
        EventType = eventType,
        ActorId = Guid.NewGuid(),
        ActorPersona = "P-01",
        EntityType = nameof(TenantUser),
        EntityId = entityId,
        NewValue = new { marker },
        OccurredAtUtc = clock.GetUtcNow(),
        CorrelationId = Guid.NewGuid(),
    };

    /// <summary>Counts events for an entity whose <c>new_value</c> jsonb carries the given marker.</summary>
    private async Task<int> CountWithMarkerAsync(Guid entityId, Guid marker)
    {
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM event_log WHERE entity_id = @e AND new_value::text LIKE @m", connection);
        command.Parameters.AddWithValue("e", entityId);
        command.Parameters.AddWithValue("m", $"%{marker}%");
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
