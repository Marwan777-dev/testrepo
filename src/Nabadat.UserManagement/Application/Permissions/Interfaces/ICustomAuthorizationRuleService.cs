using Nabadat.UserManagement.Domain.Entities;

namespace Nabadat.UserManagement.Application.Permissions.Interfaces;

/// <summary>
/// Per-user custom authorization rules (US3) over <c>custom_authorization_rules</c> (tenant
/// schema, EF / <c>TenantDbContext</c>). Implemented by <c>CustomAuthorizationRuleService</c>.
/// Exposes the read consumed by the permission-evaluation/data-scope read paths plus the
/// create/update/delete use cases — each of which bumps the target user's permission-snapshot
/// version and co-writes an audit event in one transaction (the raw EF CRUD primitives are
/// internal to the implementation).
/// </summary>
public interface ICustomAuthorizationRuleService
{
    /// <summary>All custom rules for a user, oldest first.</summary>
    Task<IReadOnlyList<CustomAuthorizationRule>> GetRulesForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Creates a rule, bumps the user's snapshot version, and audits — atomically.</summary>
    Task<CustomAuthorizationRule> CreateRuleAsync(
        Guid actorId,
        string actorPersona,
        Guid targetUserId,
        IReadOnlyList<string> allowedActions,
        IReadOnlyDictionary<string, IReadOnlyList<string>> parameterScopeAssignments,
        CancellationToken ct = default);

    /// <summary>Replaces a rule's grants, bumps the user's snapshot version, and audits — atomically.</summary>
    Task<CustomAuthorizationRule> UpdateRuleAsync(
        Guid actorId,
        string actorPersona,
        Guid targetUserId,
        Guid ruleId,
        IReadOnlyList<string> allowedActions,
        IReadOnlyDictionary<string, IReadOnlyList<string>> parameterScopeAssignments,
        CancellationToken ct = default);

    /// <summary>Deletes a rule, bumps the user's snapshot version, and audits — atomically.</summary>
    Task DeleteRuleAsync(
        Guid actorId,
        string actorPersona,
        Guid targetUserId,
        Guid ruleId,
        CancellationToken ct = default);
}
