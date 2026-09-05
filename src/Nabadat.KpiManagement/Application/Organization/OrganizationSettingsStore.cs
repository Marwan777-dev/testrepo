using Microsoft.EntityFrameworkCore;
using Nabadat.KpiManagement.Application.Events;
using Nabadat.KpiManagement.Application.Interfaces;
using Nabadat.KpiManagement.Application.Organization.Interfaces;
using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Application.Organization;

/// <summary>
/// <see cref="IOrganizationSettingsStore"/> over M-06's <see cref="ITenantDbContext"/> (tenant DB).
/// Each write runs inside one <see cref="ITenantDbContext.ExecuteAsync(Func{Task}, CancellationToken)"/>
/// transaction so the row update and its M-17 <c>settings.changed</c> audit row commit together
/// (data-model.md §8). No-op writes (payload equals current state) skip both.
/// </summary>
public sealed class OrganizationSettingsStore : IOrganizationSettingsStore
{
    private readonly ITenantDbContext _context;
    private readonly KpiEventPublisher _events;

    public OrganizationSettingsStore(ITenantDbContext context, KpiEventPublisher events)
    {
        _context = context;
        _events = events;
    }

    public Task<OrganizationSettings?> GetAsync(CancellationToken ct = default) =>
        _context.OrganizationSettings.OrderBy(o => o.CreatedAt).FirstOrDefaultAsync(ct);

    public async Task<OrganizationSettings> UpdateAsync(
        string name,
        string industry,
        Guid actorId,
        string actorPersona,
        Guid correlationId,
        DateTimeOffset occurredAt,
        CancellationToken ct = default)
    {
        var settings = await RequireSingletonAsync(ct);

        // No-op: payload matches current state → no write, no event (settings-api.md).
        if (string.Equals(settings.Name, name, StringComparison.Ordinal)
            && string.Equals(settings.Industry, industry, StringComparison.Ordinal))
        {
            return settings;
        }

        var diff = new Dictionary<string, object?>();
        if (!string.Equals(settings.Name, name, StringComparison.Ordinal)) diff["name"] = name;
        if (!string.Equals(settings.Industry, industry, StringComparison.Ordinal)) diff["industry"] = industry;

        var before = Snapshot(settings);

        await _context.ExecuteAsync(async () =>
        {
            settings.Name = name;
            settings.Industry = industry;
            settings.UpdatedAt = occurredAt;
            settings.UpdatedBy = actorId;

            await _events.PublishOrganizationSettingsChangedAsync(
                settings.Id, actorId, actorPersona,
                before,
                new { action = "updated", changes = diff },
                occurredAt, correlationId, ct);
        }, ct);

        return settings;
    }

    public async Task<OrganizationSettings> UpdateLogoRefAsync(
        string blobRef,
        Guid actorId,
        string actorPersona,
        Guid correlationId,
        DateTimeOffset occurredAt,
        CancellationToken ct = default)
    {
        var settings = await RequireSingletonAsync(ct);

        if (string.Equals(settings.LogoBlobRef, blobRef, StringComparison.Ordinal))
        {
            return settings;
        }

        var fromRef = settings.LogoBlobRef;

        await _context.ExecuteAsync(async () =>
        {
            settings.LogoBlobRef = blobRef;
            settings.UpdatedAt = occurredAt;
            settings.UpdatedBy = actorId;

            await _events.PublishOrganizationSettingsChangedAsync(
                settings.Id, actorId, actorPersona,
                new { logo_blob_ref = fromRef },
                new { action = "logo_replaced", from_blob_ref = fromRef, to_blob_ref = blobRef },
                occurredAt, correlationId, ct);
        }, ct);

        return settings;
    }

    private async Task<OrganizationSettings> RequireSingletonAsync(CancellationToken ct) =>
        await GetAsync(ct)
        ?? throw new InvalidOperationException("Organization settings row is missing for this tenant.");

    private static object Snapshot(OrganizationSettings s) => new
    {
        name = s.Name,
        industry = s.Industry,
        logo_blob_ref = s.LogoBlobRef,
    };
}
