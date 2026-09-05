using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Hierarchy;
using Nabadat.UserManagement.Application.Hierarchy.Interfaces;
using Nabadat.UserManagement.Application.Permissions;
using Nabadat.UserManagement.Application.Permissions.Exceptions;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.UnitTests.TestSupport;
using Npgsql;
using NSubstitute;
using Xunit;

namespace Nabadat.UserManagement.UnitTests.Permissions;

/// <summary>
/// T069 — write-first unit tests for the data-layer authorization of permission
/// module assignment and enforcement (US2). Drives the contracts of
/// <c>PermissionAssignmentService</c> (T078), <c>DataLayerAuthorizationGuard</c>
/// (T079), <c>ForbiddenException</c>, and <c>PermissionEvaluationService</c> (T081).
///
/// Authority split (FR-007 / spec clarification): P-01 (CX Program Manager) holds
/// exclusive authority over the 7 CX-domain modules; P-07 may assign only the
/// non-CX modules (User Management, Tenant Configuration). Enforcement is at the
/// service/data layer — not merely the UI — so it holds even when the controller is
/// bypassed. The default-deny rule means a user with no module assignment has zero
/// access, and a revoked grant must not survive the next session refresh.
///
/// These types do not exist yet, so this project fails to COMPILE — the valid red
/// state for a write-first story whose production type is not yet scaffolded
/// (CLAUDE.md Unit Test Policy, rule 7).
/// </summary>
public sealed class PermissionAssignmentServiceTests
{
    // Canonical DOC-02 module ids (the catalogue itself is out of M-10's scope).
    private const string SurveyBuilder = "SurveyBuilder";   // CX-domain — P-01 only
    private const string UserManagement = "UserManagement"; // non-CX — P-07 allowed

    private const string P01 = "P-01";
    private const string P07 = "P-07";

    private readonly IPermissionModuleAssignmentService _permissions = Substitute.For<IPermissionModuleAssignmentService>();
    private readonly ITenantUserService _users = Substitute.For<ITenantUserService>();
    private readonly IUserManagementEventPublisher _events = Substitute.For<IUserManagementEventPublisher>();
    private readonly FakeTimeProvider _clock = new();

    // The guard is the audited data-layer enforcement point: on denial it emits a
    // permission.forbidden_attempt event (verified by DataLayerAuthorizationGuardTests, T072)
    // and throws. Constructed real here so the assignment service's enforcement path is exercised.
    private readonly DataLayerAuthorizationGuard _guard;

    public PermissionAssignmentServiceTests() =>
        _guard = new DataLayerAuthorizationGuard(_events, new FakeTenantDbContext(), _clock);

    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();

    private PermissionAssignmentService CreateSut() => new(
        _permissions, _users, _guard, _events, new FakeTenantDbContext(), _clock);

    // The evaluator gains US3 scope dependencies (T105). For these module-only checks
    // they are wired with empty-returning stubs so behaviour is unchanged: no scope
    // assignments, no custom rules, and no hierarchy node on the (unstubbed) user.
    private PermissionEvaluationService CreateEvaluator()
    {
        var scopeRepo = Substitute.For<IDataScopeService>();
        scopeRepo.GetScopeAssignmentsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<DataScopeAssignment>());
        var hierarchyReader = Substitute.For<IOrganizationHierarchyService>();
        var customRules = Substitute.For<ICustomAuthorizationRuleService>();
        customRules.GetRulesForUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CustomAuthorizationRule>());

        var hierarchy = new HierarchyCascadeService(hierarchyReader);
        var dataScope = new DataScopeRuleService(scopeRepo, _users, _events, new FakeTenantDbContext(), _clock);
        return new PermissionEvaluationService(_permissions, _users, hierarchy, dataScope, customRules);
    }

    // ── Case 1: P-07 assigning a CX-domain module is rejected at the data layer ──
    [Fact]
    public async Task AssignModule_throws_forbidden_and_skips_write_when_p07_assigns_cx_domain_module()
    {
        var act = () => CreateSut().AssignModuleAsync(_actorId, P07, _targetUserId, SurveyBuilder, ["View"]);

        await act.Should().ThrowAsync<ForbiddenException>();
        // "before DB write" — the guard runs first, so nothing is persisted.
        await _permissions.DidNotReceive().ReplaceAssignmentsAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<PermissionModuleAssignment>>(),
            Arg.Any<CancellationToken>());
    }

    // ── Case 2: P-07 assigning a non-CX module succeeds and persists ──
    [Fact]
    public async Task AssignModule_persists_and_bumps_snapshot_version_when_p07_assigns_user_management()
    {
        _permissions.GetAssignmentsAsync(_targetUserId, Arg.Any<CancellationToken>())
            .Returns([]);
        _users.GetByIdAsync(_targetUserId, Arg.Any<CancellationToken>())
            .Returns(new TenantUser { UserId = _targetUserId, Persona = "P-03", LastPermissionSnapshotVersion = 3 });

        await CreateSut().AssignModuleAsync(_actorId, P07, _targetUserId, UserManagement, ["Full"]);

        await _permissions.Received().ReplaceAssignmentsAsync(
            _targetUserId,
            Arg.Is<IReadOnlyList<PermissionModuleAssignment>>(a => a.Any(x => x.ModuleId == UserManagement)),
            Arg.Any<CancellationToken>());
        // Permission change invalidates in-flight snapshots at the next refresh (FR-013).
        await _users.Received().UpdateAsync(
            Arg.Is<TenantUser>(u => u.LastPermissionSnapshotVersion == 4),
            Arg.Any<CancellationToken>());
        await _events.Received().PublishAsync(
            Arg.Is<UserManagementEvent>(e => e.EventType == "permission.modified"),
            Arg.Any<CancellationToken>());
    }

    // ── Case 3: P-01 assigning a CX-domain module succeeds ──
    [Fact]
    public async Task AssignModule_persists_assignment_when_p01_assigns_cx_domain_module()
    {
        _permissions.GetAssignmentsAsync(_targetUserId, Arg.Any<CancellationToken>())
            .Returns([]);
        _users.GetByIdAsync(_targetUserId, Arg.Any<CancellationToken>())
            .Returns(new TenantUser { UserId = _targetUserId, Persona = "P-03" });

        await CreateSut().AssignModuleAsync(_actorId, P01, _targetUserId, SurveyBuilder, ["View", "Manage"]);

        await _permissions.Received().ReplaceAssignmentsAsync(
            _targetUserId,
            Arg.Is<IReadOnlyList<PermissionModuleAssignment>>(a => a.Any(x => x.ModuleId == SurveyBuilder)),
            Arg.Any<CancellationToken>());
    }

    // ── Case 4: default-deny — no assignment means no access ──
    [Fact]
    public async Task CheckPermission_returns_denied_when_no_survey_builder_assignment_exists()
    {
        _permissions.GetAssignmentsAsync(_targetUserId, Arg.Any<CancellationToken>())
            .Returns([]);

        var decision = await CreateEvaluator().CheckPermissionAsync(_targetUserId, "CreateSurvey");

        decision.IsAllowed.Should().BeFalse();
    }

    // ── Case 5: a revoked grant must not survive the next session refresh ──
    [Fact]
    public async Task CheckPermission_returns_denied_after_assignment_removal_and_session_refresh()
    {
        var evaluator = CreateEvaluator();

        // Granted: the snapshot is rebuilt from current assignments, so the action is allowed.
        _permissions.GetAssignmentsAsync(_targetUserId, Arg.Any<CancellationToken>())
            .Returns([GrantedModule(SurveyBuilder, "View", "Manage", "Full")]);
        var whileGranted = await evaluator.CheckPermissionAsync(_targetUserId, "CreateSurvey");
        whileGranted.IsAllowed.Should().BeTrue();

        // Revoked + refreshed: the rebuilt snapshot no longer carries the module → denied.
        _permissions.GetAssignmentsAsync(_targetUserId, Arg.Any<CancellationToken>())
            .Returns([]);
        var afterRevoke = await evaluator.CheckPermissionAsync(_targetUserId, "CreateSurvey");
        afterRevoke.IsAllowed.Should().BeFalse();
    }

    private PermissionModuleAssignment GrantedModule(string moduleId, params string[] modes) => new()
    {
        AssignmentId = Guid.NewGuid(),
        UserId = _targetUserId,
        ModuleId = moduleId,
        AllowedModes = modes,
        AssignedBy = _actorId,
        CreatedAt = _clock.GetUtcNow(),
        UpdatedAt = _clock.GetUtcNow(),
    };
}
