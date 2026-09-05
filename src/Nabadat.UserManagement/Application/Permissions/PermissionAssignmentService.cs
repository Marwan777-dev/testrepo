using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;

namespace Nabadat.UserManagement.Application.Permissions;

/// <summary>
/// Assigns permission modules to a user (T078): either a single module
/// (<see cref="AssignModuleAsync"/>) or a full replacement of the user's module set
/// (<see cref="ReplacePermissionsAsync"/>, backing <c>PUT /users/{id}/permissions</c>).
/// Every targeted module is run through <see cref="DataLayerAuthorizationGuard"/>
/// first — a P-07 actor cannot touch a CX-domain module, and the check runs before
/// any write. On success the assignments are replaced atomically, the user's
/// <c>LastPermissionSnapshotVersion</c> is bumped (so in-flight sessions rebuild their
/// snapshot on the next refresh, FR-013), and a <c>permission.modified</c> event is
/// co-written to M-17 in the same transaction.
/// </summary>
public sealed class PermissionAssignmentService
{
    private readonly IPermissionModuleAssignmentService _permissions;
    private readonly ITenantUserService _users;
    private readonly DataLayerAuthorizationGuard _guard;
    private readonly IUserManagementEventPublisher _events;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _clock;

    public PermissionAssignmentService(
        IPermissionModuleAssignmentService permissions,
        ITenantUserService users,
        DataLayerAuthorizationGuard guard,
        IUserManagementEventPublisher events,
        ITenantDbContext context,
        TimeProvider clock)
    {
        _permissions = permissions;
        _users = users;
        _guard = guard;
        _events = events;
        _context = context;
        _clock = clock;
    }

    /// <summary>
    /// Grants <paramref name="moduleId"/> at <paramref name="allowedModes"/> to
    /// <paramref name="targetUserId"/>, replacing any prior grant for that one module
    /// while leaving the user's other assignments intact. Throws
    /// <see cref="Exceptions.ForbiddenException"/> (before any write) when the actor
    /// may not assign the module.
    /// </summary>
    public async Task AssignModuleAsync(
        Guid actorId,
        string actorPersona,
        Guid targetUserId,
        string moduleId,
        IReadOnlyList<string> allowedModes,
        CancellationToken ct = default)
    {
        // Data-layer authority check — runs (and audits any denial) before any DB write.
        await _guard.EnforceCanAssignModuleAsync(actorId, actorPersona, moduleId, ct);

        var now = _clock.GetUtcNow();
        var existing = await _permissions.GetAssignmentsAsync(targetUserId, ct);

        // Replace-or-add: drop any prior grant for this module, then add the new one.
        var updated = existing
            .Where(a => a.ModuleId != moduleId)
            .Append(NewAssignment(targetUserId, moduleId, allowedModes, actorId, now))
            .ToList();

        // oldValue = the grant this assignment replaces (null when the module was unassigned).
        var oldValue = existing
            .Where(a => a.ModuleId == moduleId)
            .Select(a => new { a.ModuleId, a.AllowedModes })
            .FirstOrDefault();

        await CommitAsync(now, actorId, actorPersona, targetUserId, updated, oldValue, new { moduleId, allowedModes }, ct);
    }

    /// <summary>
    /// Replaces the user's entire set of permission module assignments with
    /// <paramref name="assignments"/> (backs <c>PUT /users/{id}/permissions</c>). Every
    /// module in the new set is authorised first, so a P-07 actor including any
    /// CX-domain module is rejected (<see cref="Exceptions.ForbiddenException"/>) before
    /// any write. Modules absent from <paramref name="assignments"/> are revoked.
    /// </summary>
    public async Task ReplacePermissionsAsync(
        Guid actorId,
        string actorPersona,
        Guid targetUserId,
        IReadOnlyList<PermissionModuleAssignment> assignments,
        CancellationToken ct = default)
    {
        // Authorise every target module BEFORE any write (audits + throws on denial).
        foreach (var assignment in assignments)
        {
            await _guard.EnforceCanAssignModuleAsync(actorId, actorPersona, assignment.ModuleId, ct);
        }

        var now = _clock.GetUtcNow();
        var existing = await _permissions.GetAssignmentsAsync(targetUserId, ct);
        var normalized = assignments
            .Select(a => NewAssignment(targetUserId, a.ModuleId, a.AllowedModes, actorId, now))
            .ToList();

        // oldValue = the full module set being replaced (empty when the user had none).
        var oldValue = new { assignments = existing.Select(a => new { a.ModuleId, a.AllowedModes }) };
        var newValue = new { assignments = normalized.Select(a => new { a.ModuleId, a.AllowedModes }) };
        await CommitAsync(now, actorId, actorPersona, targetUserId, normalized, oldValue, newValue, ct);
    }

    private static PermissionModuleAssignment NewAssignment(
        Guid targetUserId, string moduleId, IReadOnlyList<string> allowedModes, Guid actorId, DateTimeOffset now) => new()
    {
        AssignmentId = Guid.NewGuid(),
        UserId = targetUserId,
        ModuleId = moduleId,
        AllowedModes = allowedModes,
        AssignedBy = actorId,
        CreatedAt = now,
        UpdatedAt = now,
    };

    /// <summary>
    /// Shared write path: bumps the snapshot version, replaces assignments, and emits
    /// <c>permission.modified</c> — all atomically in one unit of work.
    /// </summary>
    private async Task CommitAsync(
        DateTimeOffset now,
        Guid actorId,
        string actorPersona,
        Guid targetUserId,
        IReadOnlyList<PermissionModuleAssignment> finalAssignments,
        object? oldValue,
        object newValue,
        CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(targetUserId, ct)
            ?? throw new KeyNotFoundException($"User {targetUserId} does not exist.");

        user.LastPermissionSnapshotVersion += 1;
        user.UpdatedAt = now;

        await _context.ExecuteAsync(async () =>
        {
            await _permissions.ReplaceAssignmentsAsync(targetUserId, finalAssignments, ct);
            await _users.UpdateAsync(user, ct);
            await _events.PublishAsync(new UserManagementEvent
            {
                EventType = "permission.modified",
                ActorId = actorId,
                ActorPersona = actorPersona,
                EntityType = "PermissionModuleAssignment",
                EntityId = targetUserId,
                OldValue = oldValue,
                NewValue = newValue,
                OccurredAtUtc = now,
                CorrelationId = Guid.NewGuid(),
            }, ct);
        }, ct);
    }
}
