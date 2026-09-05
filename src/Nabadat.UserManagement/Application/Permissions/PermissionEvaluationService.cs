using Nabadat.UserManagement.Application.Hierarchy;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Domain.ValueObjects;

namespace Nabadat.UserManagement.Application.Permissions;

/// <summary>
/// <b>Published interface implementation (AD-01).</b> Decides whether a user may
/// perform an action (T081, extended for US3 scope in T105). The effective
/// <see cref="PermissionSnapshot"/> is built from the user's current module
/// assignments, custom authorization rules, parameter scope, and organization
/// hierarchy, so a revoked grant stops being exercisable as soon as the snapshot is
/// rebuilt on the next refresh (FR-013). The model is default-deny.
///
/// <para><b>Evaluation order (T105):</b> a module-level grant is checked first; a
/// fine-grained custom-rule grant is evaluated <i>after</i> the module check (it can
/// authorise an action the persona baseline does not, e.g. <c>UpdateSurvey</c> without
/// <c>DeleteSurvey</c>). Once the action is authorised, an entity-scoped request is
/// additionally constrained by the hierarchy cascade: the entity's organization node
/// must be the user's assigned node or one of its descendants
/// (<see cref="PermissionSnapshot.HierarchyDescendantIds"/>, pre-computed downward-only).</para>
///
/// The action→module map below is the enforcement-side resolver for the actions M-10
/// guards in Phase 1; the full DOC-02 action catalogue is owned elsewhere (out of
/// M-10's scope). At request time the host caches this snapshot on the session row
/// (version-checked) — that fast path layers on top of this canonical builder.
/// </summary>
public sealed class PermissionEvaluationService : IUserManagementPermissionService
{
    /// <summary>DOC-02 action → (owning module, coarse mode required to perform it).</summary>
    private static readonly IReadOnlyDictionary<string, (string Module, string Mode)> ActionCatalogue =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["CreateSurvey"] = ("SurveyBuilder", "Manage"),
            ["UpdateSurvey"] = ("SurveyBuilder", "Manage"),
            ["DeleteSurvey"] = ("SurveyBuilder", "Full"),
        };

    private readonly IPermissionModuleAssignmentService _permissions;
    private readonly ITenantUserService _users;
    private readonly HierarchyCascadeService _hierarchy;
    private readonly DataScopeRuleService _dataScope;
    private readonly ICustomAuthorizationRuleService _customRules;

    public PermissionEvaluationService(
        IPermissionModuleAssignmentService permissions,
        ITenantUserService users,
        HierarchyCascadeService hierarchy,
        DataScopeRuleService dataScope,
        ICustomAuthorizationRuleService customRules)
    {
        _permissions = permissions;
        _users = users;
        _hierarchy = hierarchy;
        _dataScope = dataScope;
        _customRules = customRules;
    }

    public async Task<PermissionDecision> CheckPermissionAsync(
        Guid userId,
        string action,
        Guid? entityId = null,
        CancellationToken ct = default)
    {
        var snapshot = await GetPermissionSnapshotAsync(userId, ct);

        // 1. Module-level check: does a held module grant this action at the required mode?
        var knownAction = ActionCatalogue.TryGetValue(action, out var required);
        var grantedByModule = knownAction
            && snapshot.Modules.TryGetValue(required.Module, out var modes)
            && modes.Contains(required.Mode);

        // 2. Custom action rules are evaluated AFTER the module-level check — a custom
        //    grant authorises a fine-grained action the baseline module does not.
        var grantedByCustomRule = snapshot.CustomActions.Contains(action);

        if (!grantedByModule && !grantedByCustomRule)
        {
            return PermissionDecision.Denied(knownAction ? "module.not_assigned" : "action.unknown");
        }

        // 3. Entity-level scope check (downward-only hierarchy cascade). Only applies to a
        //    hierarchy-scoped user acting on a specific entity; an unscoped user is unconstrained.
        if (entityId is { } targetNode && snapshot.HierarchyNodeId is { } scopeRoot)
        {
            var inScope = targetNode == scopeRoot || snapshot.HierarchyDescendantIds.Contains(targetNode);
            if (!inScope)
            {
                return PermissionDecision.Denied("scope.out_of_hierarchy");
            }
        }

        return PermissionDecision.Allowed();
    }

    public async Task<PermissionSnapshot> GetPermissionSnapshotAsync(Guid userId, CancellationToken ct = default)
    {
        var assignments = await _permissions.GetAssignmentsAsync(userId, ct);
        var modules = assignments.ToDictionary(a => a.ModuleId, a => a.AllowedModes);

        var user = await _users.GetByIdAsync(userId, ct);

        // Hierarchy cascade — pre-compute descendants so request-time checks avoid a tree walk.
        var hierarchyNodeId = user?.OrganizationNodeId;
        IReadOnlyList<Guid> descendants = hierarchyNodeId is { } nodeId
            ? await _hierarchy.GetDescendantNodeIdsAsync(nodeId, ct)
            : [];

        // Parameter scope — data-layer filters read this from the snapshot.
        var scope = new Dictionary<string, IReadOnlyList<string>>(await _dataScope.GetScopeMapAsync(userId, ct));

        // Custom authorization rules — fine-grained action grants + any extra parameter scope.
        var rules = await _customRules.GetRulesForUserAsync(userId, ct);
        var customActions = rules.SelectMany(r => r.AllowedActions).Distinct(StringComparer.Ordinal).ToList();
        foreach (var grant in rules.SelectMany(r => r.ParameterScopeAssignments))
        {
            scope[grant.Key] = scope.TryGetValue(grant.Key, out var existing)
                ? existing.Union(grant.Value, StringComparer.Ordinal).ToList()
                : grant.Value;
        }

        return new PermissionSnapshot
        {
            Version = user?.LastPermissionSnapshotVersion ?? 0,
            Modules = modules,
            CustomActions = customActions,
            ScopeAssignments = scope,
            HierarchyNodeId = hierarchyNodeId,
            HierarchyDescendantIds = descendants,
        };
    }
}
