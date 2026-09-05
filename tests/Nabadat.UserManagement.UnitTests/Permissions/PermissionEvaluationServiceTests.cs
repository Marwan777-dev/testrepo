using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Hierarchy;
using Nabadat.UserManagement.Application.Permissions;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using Nabadat.UserManagement.Application.Hierarchy.Interfaces;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;

namespace Nabadat.UserManagement.UnitTests.Permissions;

/// <summary>
/// T105 — unit tests for the US3 scope/custom-rule integration in
/// <c>PermissionEvaluationService.CheckPermissionAsync</c>: custom action rules are
/// evaluated <i>after</i> the module-level check (a custom grant authorises an action
/// the persona baseline does not — AC3), and an entity-scoped request is constrained
/// by the downward-only hierarchy cascade — the entity's node must be the user's
/// assigned node or one of its descendants (AC2).
/// </summary>
public sealed class PermissionEvaluationServiceTests
{
    private const string SurveyBuilder = "SurveyBuilder";

    // Hierarchy: RegionA (user's node) → BranchX; RegionB is a sibling.
    private static readonly Guid RegionA = Guid.NewGuid();
    private static readonly Guid RegionB = Guid.NewGuid();
    private static readonly Guid BranchX = Guid.NewGuid();

    private static readonly OrganizationHierarchyNode[] Tree =
    [
        new() { NodeId = RegionA, ParentNodeId = null, Name = "Region A", Path = "/region-a/" },
        new() { NodeId = RegionB, ParentNodeId = null, Name = "Region B", Path = "/region-b/" },
        new() { NodeId = BranchX, ParentNodeId = RegionA, Name = "Branch X", Path = "/region-a/branch-x/" },
    ];

    private readonly IPermissionModuleAssignmentService _permissions = Substitute.For<IPermissionModuleAssignmentService>();
    private readonly ITenantUserService _users = Substitute.For<ITenantUserService>();
    private readonly IDataScopeService _scopes = Substitute.For<IDataScopeService>();
    private readonly IOrganizationHierarchyService _hierarchyReader = Substitute.For<IOrganizationHierarchyService>();
    private readonly ICustomAuthorizationRuleService _customRules = Substitute.For<ICustomAuthorizationRuleService>();
    private readonly IUserManagementEventPublisher _events = Substitute.For<IUserManagementEventPublisher>();
    private readonly FakeTimeProvider _clock = new();

    private readonly Guid _userId = Guid.NewGuid();

    public PermissionEvaluationServiceTests()
    {
        _permissions.GetAssignmentsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PermissionModuleAssignment>());
        _scopes.GetScopeAssignmentsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<DataScopeAssignment>());
        _customRules.GetRulesForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CustomAuthorizationRule>());
        foreach (var node in Tree)
        {
            _hierarchyReader.GetNodeAsync(node.NodeId, Arg.Any<CancellationToken>()).Returns(node);
        }

        _hierarchyReader.GetNodesByPathPrefixAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Tree);
    }

    private PermissionEvaluationService CreateSut() => new(
        _permissions,
        _users,
        new HierarchyCascadeService(_hierarchyReader),
        new DataScopeRuleService(_scopes, _users, _events, new FakeTenantDbContext(), _clock),
        _customRules);

    private void GivenUser(Guid? organizationNodeId = null) =>
        _users.GetByIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new TenantUser { UserId = _userId, Persona = "P-03", OrganizationNodeId = organizationNodeId });

    private void GivenModule(string moduleId, params string[] modes) =>
        _permissions.GetAssignmentsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new[] { new PermissionModuleAssignment { UserId = _userId, ModuleId = moduleId, AllowedModes = modes } });

    private void GivenCustomRule(params string[] allowedActions) =>
        _customRules.GetRulesForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new[] { new CustomAuthorizationRule { UserId = _userId, AllowedActions = allowedActions } });

    // ── AC3: a custom rule grants a fine-grained action the module baseline does not ──
    [Fact]
    public async Task CheckPermission_allows_update_via_custom_rule_when_module_not_assigned()
    {
        GivenUser();
        GivenCustomRule("UpdateSurvey");  // no SurveyBuilder module assigned

        var decision = await CreateSut().CheckPermissionAsync(_userId, "UpdateSurvey");

        decision.IsAllowed.Should().BeTrue();
    }

    // ── AC3: the same custom rule must NOT grant delete (only update was allowed) ──
    [Fact]
    public async Task CheckPermission_denies_delete_when_custom_rule_grants_only_update()
    {
        GivenUser();
        GivenCustomRule("UpdateSurvey");

        var decision = await CreateSut().CheckPermissionAsync(_userId, "DeleteSurvey");

        decision.IsAllowed.Should().BeFalse();
        decision.DeniedReason.Should().Be("module.not_assigned");
    }

    // ── AC2: an entity within the user's hierarchy node's descendants is in scope ──
    [Fact]
    public async Task CheckPermission_allows_entity_within_hierarchy_descendants()
    {
        GivenUser(organizationNodeId: RegionA);
        GivenModule(SurveyBuilder, "Manage");

        var decision = await CreateSut().CheckPermissionAsync(_userId, "UpdateSurvey", entityId: BranchX);

        decision.IsAllowed.Should().BeTrue();
    }

    // ── AC2: a sibling node (not a descendant) is out of the downward-only cascade ──
    [Fact]
    public async Task CheckPermission_denies_entity_in_sibling_node_outside_hierarchy_scope()
    {
        GivenUser(organizationNodeId: RegionA);
        GivenModule(SurveyBuilder, "Manage");

        var decision = await CreateSut().CheckPermissionAsync(_userId, "UpdateSurvey", entityId: RegionB);

        decision.IsAllowed.Should().BeFalse();
        decision.DeniedReason.Should().Be("scope.out_of_hierarchy");
    }

    // ── An unscoped user (no hierarchy node) is not constrained by entity scope ──
    [Fact]
    public async Task CheckPermission_allows_entity_when_user_has_no_hierarchy_node()
    {
        GivenUser(organizationNodeId: null);
        GivenModule(SurveyBuilder, "Manage");

        var decision = await CreateSut().CheckPermissionAsync(_userId, "UpdateSurvey", entityId: RegionB);

        decision.IsAllowed.Should().BeTrue();
    }

    // ── The snapshot pre-computes descendants and surfaces custom actions ──
    [Fact]
    public async Task GetPermissionSnapshot_precomputes_descendants_and_custom_actions()
    {
        GivenUser(organizationNodeId: RegionA);
        GivenCustomRule("UpdateSurvey");

        var snapshot = await CreateSut().GetPermissionSnapshotAsync(_userId);

        snapshot.HierarchyNodeId.Should().Be(RegionA);
        snapshot.HierarchyDescendantIds.Should().Contain(BranchX).And.NotContain(RegionB);
        snapshot.CustomActions.Should().Contain("UpdateSurvey");
    }
}
