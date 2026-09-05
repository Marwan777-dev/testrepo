using Microsoft.EntityFrameworkCore;
using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Domain.Entities;

namespace Nabadat.UserManagement.Application.Permissions;

/// <summary>
/// Per-tenant persona authorization-matrix baselines (T080). This one class is both the EF
/// data-access over the control-plane <c>persona_baselines</c> table (implementing
/// <see cref="IPersonaBaselineService"/>, consumed by user provisioning, the seeder, and the
/// baselines controller) and the read/edit use cases. Editing is subject to the FR-007 split via
/// <see cref="DataLayerAuthorizationGuard"/>: a P-07 actor may put only non-CX modules into a
/// baseline. Saving an edit flips <c>IsCustomised</c> and emits a <c>persona_baseline.updated</c>
/// event. The baseline (control-plane DB) and the event (tenant <c>event_log</c>) live in different
/// databases, so the two writes are sequenced rather than wrapped in one transaction (DB-08).
/// </summary>
public sealed class PersonaBaselineService : IPersonaBaselineService
{
    private static readonly string[] AllPersonas =
        ["P-01", "P-02", "P-03", "P-04", "P-05", "P-06", "P-07", "P-08"];

    // The 7 CX-domain modules (P-01-exclusive) + the two non-CX modules P-07 may administer.
    private static readonly string[] CxDomainModules =
        ["SurveyBuilder", "ChannelManagement", "AudienceManagement", "AnalyticsAndReporting",
         "CaseManagement", "AlertsAndNotifications", "KpiConfiguration"];

    private const string UserManagement = "UserManagement";
    private const string TenantConfiguration = "TenantConfiguration";
    private const string KpiConfiguration = "KpiConfiguration";
    private static readonly string[] FullAccess = ["View", "Manage", "Full"];
    private static readonly string[] ViewOnly = ["View"];

    private readonly IControlPlaneDbContext _controlPlane;
    private readonly ITenantDbContext _tenantContext;
    private readonly DataLayerAuthorizationGuard _guard;
    private readonly IUserManagementEventPublisher _events;
    private readonly TimeProvider _clock;

    public PersonaBaselineService(
        IControlPlaneDbContext controlPlane,
        ITenantDbContext tenantContext,
        DataLayerAuthorizationGuard guard,
        IUserManagementEventPublisher events,
        TimeProvider clock)
    {
        _controlPlane = controlPlane;
        _tenantContext = tenantContext;
        _guard = guard;
        _events = events;
        _clock = clock;
    }

    // --- Reads (IPersonaBaselineService) ---

    /// <summary>Reads one persona's baseline for a tenant, or null if not seeded.</summary>
    public async Task<PersonaBaseline?> GetAsync(Guid tenantId, string personaId, CancellationToken ct = default) =>
        await _controlPlane.PersonaBaselines
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.PersonaId == personaId, ct);

    /// <summary>All persona baselines for a tenant (P-01..P-08, those that are seeded), ordered by persona id.</summary>
    public async Task<IReadOnlyList<PersonaBaseline>> GetAllBaselinesAsync(Guid tenantId, CancellationToken ct = default) =>
        await _controlPlane.PersonaBaselines
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId)
            .OrderBy(b => b.PersonaId)
            .ToListAsync(ct);

    /// <summary>The authorization-matrix default module grants for a persona (empty if unseeded).</summary>
    public async Task<IReadOnlyList<PersonaModuleAssignment>> GetDefaultPermissionsForPersonaAsync(
        Guid tenantId,
        string personaId,
        CancellationToken ct = default)
    {
        var baseline = await GetAsync(tenantId, personaId, ct);
        return baseline?.PermissionModuleAssignments ?? [];
    }

    // --- Use cases ---

    /// <summary>
    /// Replaces a persona baseline's module assignments. Each module is run through the
    /// data-layer guard first, so a P-07 actor including a CX-domain module is rejected
    /// (<see cref="Exceptions.ForbiddenException"/>) before any write. On success the baseline is
    /// marked customised (control-plane write) and a <c>persona_baseline.updated</c> event is
    /// emitted to the tenant <c>event_log</c> — the two are sequenced across databases (DB-08).
    /// </summary>
    public async Task UpdateBaselineAsync(
        Guid tenantId,
        Guid actorId,
        string actorPersona,
        string personaId,
        IReadOnlyList<PersonaModuleAssignment> assignments,
        CancellationToken ct = default)
    {
        // Authority check for every module BEFORE any read/write (audits + throws on denial).
        foreach (var assignment in assignments)
        {
            await _guard.EnforceCanAssignModuleAsync(actorId, actorPersona, assignment.ModuleId, ct);
        }

        var now = _clock.GetUtcNow();
        var existing = await GetAsync(tenantId, personaId, ct);

        // oldValue = the baseline's module set before this customisation (null on first edit).
        var oldValue = existing is null
            ? null
            : new { personaId, assignments = existing.PermissionModuleAssignments };

        var baseline = existing ?? new PersonaBaseline
        {
            BaselineId = Guid.NewGuid(),
            TenantId = tenantId,
            PersonaId = personaId,
            CreatedAt = now,
        };

        baseline.PermissionModuleAssignments = assignments;
        baseline.IsCustomised = true;
        baseline.UpdatedAt = now;

        await UpsertAsync(baseline, ct);
        await _tenantContext.ExecuteAsync(() => _events.PublishAsync(new UserManagementEvent
        {
            EventType = "persona_baseline.updated",
            ActorId = actorId,
            ActorPersona = actorPersona,
            EntityType = nameof(PersonaBaseline),
            EntityId = baseline.BaselineId,
            OldValue = oldValue,
            NewValue = new { personaId, assignments },
            OccurredAtUtc = now,
            CorrelationId = Guid.NewGuid(),
        }, ct), ct);
    }

    /// <summary>
    /// Idempotently seeds the 8 platform-default persona baselines (P-01..P-08) for a tenant at
    /// provisioning. Existing baselines are left untouched (never clobbers a tenant admin's
    /// customisation). Saves immediately on the control-plane context.
    /// </summary>
    public async Task SeedDefaultsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow();
        var existing = (await _controlPlane.PersonaBaselines
                .Where(b => b.TenantId == tenantId)
                .Select(b => b.PersonaId)
                .ToListAsync(ct))
            .ToHashSet();

        foreach (var personaId in AllPersonas)
        {
            if (existing.Contains(personaId))
            {
                continue; // idempotent — never clobber an existing (possibly customised) baseline
            }

            _controlPlane.PersonaBaselines.Add(new PersonaBaseline
            {
                BaselineId = Guid.NewGuid(),
                TenantId = tenantId,
                PersonaId = personaId,
                PermissionModuleAssignments = DefaultAssignmentsFor(personaId),
                DefaultDataScopeRules = new Dictionary<string, IReadOnlyList<string>>(),
                IsCustomised = false,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        await _controlPlane.SaveChangesAsync(ct);
    }

    // --- Internal persistence (control-plane unit of work) ---

    private async Task UpsertAsync(PersonaBaseline baseline, CancellationToken ct)
    {
        var existing = await _controlPlane.PersonaBaselines
            .FirstOrDefaultAsync(b => b.TenantId == baseline.TenantId && b.PersonaId == baseline.PersonaId, ct);

        if (existing is null)
        {
            if (baseline.BaselineId == Guid.Empty)
            {
                baseline.BaselineId = Guid.NewGuid();
            }

            _controlPlane.PersonaBaselines.Add(baseline);
        }
        else
        {
            existing.PermissionModuleAssignments = baseline.PermissionModuleAssignments;
            existing.DefaultDataScopeRules = baseline.DefaultDataScopeRules;
            existing.IsCustomised = baseline.IsCustomised;
            existing.UpdatedAt = baseline.UpdatedAt;
        }

        await _controlPlane.SaveChangesAsync(ct);
    }

    /// <summary>Platform-default module grants per persona (O-01): P-01 = every module at full
    /// access; P-07 = the two non-CX modules only; P-02 (CX Analyst) and P-06 (Executive) = the
    /// KpiConfiguration module at View only (read-only KPI inspection, FR-009 / US-7) — writes still
    /// 403 (no Manage); P-03/P-04/P-05/P-08 = default-deny (empty).</summary>
    private static IReadOnlyList<PersonaModuleAssignment> DefaultAssignmentsFor(string personaId) => personaId switch
    {
        "P-01" => CxDomainModules.Append(UserManagement).Append(TenantConfiguration)
            .Select(m => new PersonaModuleAssignment { ModuleId = m, AllowedModes = FullAccess })
            .ToList(),
        "P-07" =>
        [
            new PersonaModuleAssignment { ModuleId = UserManagement, AllowedModes = FullAccess },
            new PersonaModuleAssignment { ModuleId = TenantConfiguration, AllowedModes = FullAccess },
        ],
        "P-02" or "P-06" =>
        [
            new PersonaModuleAssignment { ModuleId = KpiConfiguration, AllowedModes = ViewOnly },
        ],
        _ => [],
    };
}
