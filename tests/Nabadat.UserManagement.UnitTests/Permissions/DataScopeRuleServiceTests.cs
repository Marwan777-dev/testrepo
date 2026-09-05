using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
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
/// T097 — write-first unit tests for <c>DataScopeRuleService</c> (T102, US3). The
/// service answers two questions: <i>what parameter values may this user see</i>
/// (<c>EvaluateDataScopeAsync</c>, read from <c>data_scope_assignments</c>), and
/// <i>may these scope values be assigned</i> (<c>AssignScopeAsync</c>, which validates
/// every value against the M-13-supplied <c>data_scope_parameter_definitions</c>
/// before any write and co-writes a <c>scope.assigned</c> audit event in the same
/// transaction, FR-015).
///
/// Per the spec clarification, M-10 stores M-13 parameter <i>definitions</i> through
/// the <c>M13ParameterContractAdapter</c> (covered by
/// <see cref="M13ParameterContractAdapterTests"/>), not this service — so the
/// definition-storage validation case lives there. Here we test the rule service's
/// own contract: value-against-definition validation on assignment.
///
/// These production types do not exist yet, so this project fails to COMPILE — the
/// valid red state for a write-first story whose types are not yet scaffolded
/// (CLAUDE.md Unit Test Policy, rule 7).
/// </summary>
public sealed class DataScopeRuleServiceTests
{
    private const string Branch = "branch";
    private const string P01 = "P-01";

    private readonly IDataScopeService _scopes = Substitute.For<IDataScopeService>();
    private readonly ITenantUserService _users = Substitute.For<ITenantUserService>();
    private readonly IUserManagementEventPublisher _events = Substitute.For<IUserManagementEventPublisher>();
    private readonly FakeTimeProvider _clock = new();

    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();

    private DataScopeRuleService CreateSut() => new(
        _scopes, _users, _events, new FakeTenantDbContext(), _clock);

    private static DataScopeParameterDefinition BranchDefinition(params string[] values) => new()
    {
        ParameterName = Branch,
        Label = "Branch",
        AllowedValues = values,
        SourceModule = "M-13",
    };

    // ── Case 1: a scoped user sees exactly the values assigned to them ──
    [Fact]
    public async Task EvaluateDataScope_returns_only_assigned_values_when_user_scoped_to_riyadh_and_dammam()
    {
        _scopes.GetScopeAssignmentsAsync(_targetUserId, Arg.Any<CancellationToken>())
            .Returns(new DataScopeAssignment[]
            {
                new() { UserId = _targetUserId, ParameterName = Branch, AllowedValues = ["Riyadh", "Dammam"] },
            });

        var allowed = await CreateSut().EvaluateDataScopeAsync(_targetUserId, Branch);

        allowed.Should().BeEquivalentTo(["Riyadh", "Dammam"]);
        // "value not in allowedValues → excluded" — Jeddah was never assigned.
        allowed.Should().NotContain("Jeddah");
    }

    // ── Case 2: an unscoped parameter yields no permitted values (default-deny) ──
    [Fact]
    public async Task EvaluateDataScope_returns_empty_when_parameter_has_no_assignment()
    {
        _scopes.GetScopeAssignmentsAsync(_targetUserId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<DataScopeAssignment>());

        var allowed = await CreateSut().EvaluateDataScopeAsync(_targetUserId, Branch);

        allowed.Should().BeEmpty();
    }

    // ── Case 3: a value outside the parameter definition is rejected before any write ──
    [Fact]
    public async Task AssignScope_throws_validation_and_skips_write_when_value_not_in_parameter_definition()
    {
        _scopes.GetParameterDefinitionsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { BranchDefinition("Riyadh", "Jeddah", "Dammam") });

        var assignments = new DataScopeAssignment[]
        {
            // "Mecca" is not part of the branch parameter's allowed values.
            new() { UserId = _targetUserId, ParameterName = Branch, AllowedValues = ["Riyadh", "Mecca"] },
        };

        var act = () => CreateSut().AssignScopeAsync(_actorId, P01, _targetUserId, assignments);

        await act.Should().ThrowAsync<ValidationException>();
        await _scopes.DidNotReceive().ReplaceScopeAssignmentsAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<DataScopeAssignment>>(),
            Arg.Any<CancellationToken>());
    }

    // ── Case 4: a valid assignment persists, bumps the snapshot version, and audits ──
    [Fact]
    public async Task AssignScope_persists_bumps_snapshot_and_publishes_scope_assigned_when_values_valid()
    {
        _scopes.GetParameterDefinitionsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { BranchDefinition("Riyadh", "Jeddah", "Dammam") });
        _users.GetByIdAsync(_targetUserId, Arg.Any<CancellationToken>())
            .Returns(new TenantUser { UserId = _targetUserId, Persona = "P-03", LastPermissionSnapshotVersion = 7 });

        var assignments = new DataScopeAssignment[]
        {
            new() { UserId = _targetUserId, ParameterName = Branch, AllowedValues = ["Riyadh", "Dammam"] },
        };

        await CreateSut().AssignScopeAsync(_actorId, P01, _targetUserId, assignments);

        await _scopes.Received().ReplaceScopeAssignmentsAsync(
            _targetUserId,
            Arg.Is<IReadOnlyList<DataScopeAssignment>>(a => a.Any(x => x.ParameterName == Branch)),
            Arg.Any<CancellationToken>());
        // Scope change invalidates in-flight snapshots at the next refresh (FR-013).
        await _users.Received().UpdateAsync(
            Arg.Is<TenantUser>(u => u.LastPermissionSnapshotVersion == 8),
            Arg.Any<CancellationToken>());
        await _events.Received().PublishAsync(
            Arg.Is<UserManagementEvent>(e => e.EventType == "scope.assigned"),
            Arg.Any<CancellationToken>());
    }
}
