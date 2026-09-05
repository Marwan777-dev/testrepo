using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Permissions.Exceptions;

namespace Nabadat.UserManagement.Application.Permissions;

/// <summary>
/// The single data-layer authorization checkpoint every service calls before
/// mutating a user's or persona's permission modules (T079). Enforces the FR-007
/// authority split: P-07 (Tenant Administrator) may assign only the two non-CX
/// modules (User Management, Tenant Configuration); the 7 CX-domain modules are
/// P-01-exclusive. The check runs at the service layer, so it holds even when the
/// controller/middleware is bypassed. On denial it audits the attempt
/// (<c>permission.forbidden_attempt</c> → M-17) and throws
/// <see cref="ForbiddenException"/>; the allow path is silent.
/// </summary>
public sealed class DataLayerAuthorizationGuard
{
    /// <summary>The 7 CX-domain modules only P-01 may assign or modify (DOC-02 canonical ids).</summary>
    private static readonly IReadOnlySet<string> CxDomainModules = new HashSet<string>
    {
        "SurveyBuilder",
        "ChannelManagement",
        "AudienceManagement",
        "AnalyticsAndReporting",
        "CaseManagement",
        "AlertsAndNotifications",
        "KpiConfiguration",
    };

    private const string TenantAdministrator = "P-07";

    private readonly IUserManagementEventPublisher _events;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _clock;

    public DataLayerAuthorizationGuard(IUserManagementEventPublisher events, ITenantDbContext context, TimeProvider clock)
    {
        _events = events;
        _context = context;
        _clock = clock;
    }

    /// <summary>
    /// Throws <see cref="ForbiddenException"/> (after auditing a
    /// <c>permission.forbidden_attempt</c>) when <paramref name="actorPersona"/> may
    /// not assign <paramref name="moduleId"/> — i.e. a P-07 actor targeting a
    /// CX-domain module. Returns silently when the assignment is permitted.
    /// </summary>
    public async Task EnforceCanAssignModuleAsync(
        Guid actorId,
        string actorPersona,
        string moduleId,
        CancellationToken ct = default)
    {
        if (actorPersona != TenantAdministrator || !CxDomainModules.Contains(moduleId))
        {
            return;
        }

        var now = _clock.GetUtcNow();
        await _context.ExecuteAsync(() => _events.PublishAsync(new UserManagementEvent
        {
            EventType = "permission.forbidden_attempt",
            ActorId = actorId,
            ActorPersona = actorPersona,
            EntityType = "PermissionModule",
            EntityId = Guid.Empty,
            NewValue = new { moduleId },
            OccurredAtUtc = now,
            CorrelationId = Guid.NewGuid(),
        }, ct), ct);

        throw new ForbiddenException(
            $"Persona {actorPersona} may not assign the CX-domain module '{moduleId}'.",
            "permissions.forbidden_module");
    }
}
