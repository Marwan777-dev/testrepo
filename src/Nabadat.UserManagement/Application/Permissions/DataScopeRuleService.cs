using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Permissions.Exceptions;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Entities;

namespace Nabadat.UserManagement.Application.Permissions;

/// <summary>
/// Parameter-based data scope (T102, US3). Reads what values a user may see
/// (<see cref="EvaluateDataScopeAsync"/>) and assigns scope to a user
/// (<see cref="AssignScopeAsync"/>). On assignment every requested value is checked
/// against the M-13-supplied <c>data_scope_parameter_definitions</c> <b>before any
/// write</b>: an unknown parameter or a value outside its definition is rejected with
/// a <see cref="ValidationException"/> and nothing is persisted. A valid assignment is
/// replaced atomically, the user's <c>LastPermissionSnapshotVersion</c> is bumped so
/// in-flight sessions rebuild their snapshot on the next refresh (FR-013), and a
/// <c>scope.assigned</c> event is co-written to M-17 in the same transaction (FR-015).
/// </summary>
public sealed class DataScopeRuleService
{
    private readonly IDataScopeService _scopes;
    private readonly ITenantUserService _users;
    private readonly IUserManagementEventPublisher _events;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _clock;

    public DataScopeRuleService(
        IDataScopeService scopes,
        ITenantUserService users,
        IUserManagementEventPublisher events,
        ITenantDbContext context,
        TimeProvider clock)
    {
        _scopes = scopes;
        _users = users;
        _events = events;
        _context = context;
        _clock = clock;
    }

    /// <summary>
    /// The allowed values granted to <paramref name="userId"/> for
    /// <paramref name="parameterName"/>; empty when the parameter is unscoped for the
    /// user (default-deny — no assignment means no permitted values).
    /// </summary>
    public async Task<IReadOnlyList<string>> EvaluateDataScopeAsync(
        Guid userId,
        string parameterName,
        CancellationToken ct = default)
    {
        var assignments = await _scopes.GetScopeAssignmentsAsync(userId, ct);
        var match = assignments.FirstOrDefault(
            a => string.Equals(a.ParameterName, parameterName, StringComparison.Ordinal));
        return match?.AllowedValues ?? [];
    }

    /// <summary>
    /// The user's full parameter → allowed-values map, for caching in the
    /// <c>PermissionSnapshot.ScopeAssignments</c> (consumed by data-layer scope filters).
    /// </summary>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetScopeMapAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var assignments = await _scopes.GetScopeAssignmentsAsync(userId, ct);
        return assignments.ToDictionary(a => a.ParameterName, a => a.AllowedValues);
    }

    /// <summary>
    /// Replaces <paramref name="targetUserId"/>'s scope assignments with
    /// <paramref name="assignments"/>. Validates every parameter/value against the
    /// stored definitions first; throws <see cref="ValidationException"/> (before any
    /// write) on an unknown parameter or an out-of-range value.
    /// </summary>
    public async Task AssignScopeAsync(
        Guid actorId,
        string actorPersona,
        Guid targetUserId,
        IReadOnlyList<DataScopeAssignment> assignments,
        CancellationToken ct = default)
    {
        await ValidateAgainstDefinitionsAsync(assignments, ct);

        var now = _clock.GetUtcNow();
        var user = await _users.GetByIdAsync(targetUserId, ct)
            ?? throw new KeyNotFoundException($"User {targetUserId} does not exist.");

        // oldValue = the scope set being replaced (empty when the user had none).
        var existing = await _scopes.GetScopeAssignmentsAsync(targetUserId, ct);
        var oldValue = new { assignments = existing.Select(a => new { a.ParameterName, a.AllowedValues }) };

        user.LastPermissionSnapshotVersion += 1;
        user.UpdatedAt = now;

        var normalized = Normalize(targetUserId, assignments, now);

        await _context.ExecuteAsync(async () =>
        {
            await _scopes.ReplaceScopeAssignmentsAsync(targetUserId, normalized, ct);
            await _users.UpdateAsync(user, ct);
            await _events.PublishAsync(new UserManagementEvent
            {
                EventType = "scope.assigned",
                ActorId = actorId,
                ActorPersona = actorPersona,
                EntityType = "DataScopeAssignment",
                EntityId = targetUserId,
                OldValue = oldValue,
                NewValue = new { assignments = normalized.Select(a => new { a.ParameterName, a.AllowedValues }) },
                OccurredAtUtc = now,
                CorrelationId = Guid.NewGuid(),
            }, ct);
        }, ct);
    }

    /// <summary>
    /// Replaces a user's hierarchy node <i>and</i> parameter scope assignments in one
    /// transaction (backs <c>PUT /api/v1/users/{id}/scope</c>). Validates every value
    /// against the parameter definitions first (throws <see cref="ValidationException"/>
    /// before any write), sets <c>OrganizationNodeId</c> (a <c>null</c> clears the
    /// hierarchy scope), bumps the snapshot version, and publishes <c>scope.assigned</c>.
    /// </summary>
    public async Task ReplaceUserScopeAsync(
        Guid actorId,
        string actorPersona,
        Guid targetUserId,
        Guid? organizationNodeId,
        IReadOnlyList<DataScopeAssignment> assignments,
        CancellationToken ct = default)
    {
        await ValidateAgainstDefinitionsAsync(assignments, ct);

        var now = _clock.GetUtcNow();
        var user = await _users.GetByIdAsync(targetUserId, ct)
            ?? throw new KeyNotFoundException($"User {targetUserId} does not exist.");

        // oldValue = the hierarchy node + scope set being replaced.
        var existing = await _scopes.GetScopeAssignmentsAsync(targetUserId, ct);
        var oldValue = new
        {
            organizationNodeId = user.OrganizationNodeId,
            assignments = existing.Select(a => new { a.ParameterName, a.AllowedValues }),
        };

        user.OrganizationNodeId = organizationNodeId;
        user.LastPermissionSnapshotVersion += 1;
        user.UpdatedAt = now;

        var normalized = Normalize(targetUserId, assignments, now);

        await _context.ExecuteAsync(async () =>
        {
            await _scopes.ReplaceScopeAssignmentsAsync(targetUserId, normalized, ct);
            await _users.UpdateAsync(user, ct);
            await _events.PublishAsync(new UserManagementEvent
            {
                EventType = "scope.assigned",
                ActorId = actorId,
                ActorPersona = actorPersona,
                EntityType = "DataScopeAssignment",
                EntityId = targetUserId,
                OldValue = oldValue,
                NewValue = new
                {
                    organizationNodeId,
                    assignments = normalized.Select(a => new { a.ParameterName, a.AllowedValues }),
                },
                OccurredAtUtc = now,
                CorrelationId = Guid.NewGuid(),
            }, ct);
        }, ct);
    }

    private static IReadOnlyList<DataScopeAssignment> Normalize(
        Guid targetUserId, IReadOnlyList<DataScopeAssignment> assignments, DateTimeOffset now) =>
        assignments.Select(a => new DataScopeAssignment
        {
            AssignmentId = a.AssignmentId == Guid.Empty ? Guid.NewGuid() : a.AssignmentId,
            UserId = targetUserId,
            ParameterName = a.ParameterName,
            AllowedValues = a.AllowedValues,
            CreatedAt = now,
            UpdatedAt = now,
        }).ToList();

    private async Task ValidateAgainstDefinitionsAsync(
        IReadOnlyList<DataScopeAssignment> assignments,
        CancellationToken ct)
    {
        var definitions = (await _scopes.GetParameterDefinitionsAsync(ct)).ToDictionary(
            d => d.ParameterName,
            d => new HashSet<string>(d.AllowedValues, StringComparer.Ordinal),
            StringComparer.Ordinal);

        var failures = new List<ValidationFailure>();
        for (var i = 0; i < assignments.Count; i++)
        {
            var assignment = assignments[i];
            if (!definitions.TryGetValue(assignment.ParameterName, out var allowed))
            {
                failures.Add(new ValidationFailure($"dataScopeAssignments[{i}].parameterName", "parameter.not_found"));
                continue;
            }

            if (assignment.AllowedValues.Any(value => !allowed.Contains(value)))
            {
                failures.Add(new ValidationFailure($"dataScopeAssignments[{i}].allowedValues", "value.not_allowed"));
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }
}
