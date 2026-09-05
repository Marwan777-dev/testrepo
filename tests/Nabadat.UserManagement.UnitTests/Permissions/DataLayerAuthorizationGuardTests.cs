using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using Nabadat.UserManagement.Application.Permissions;
using Nabadat.UserManagement.Application.Permissions.Exceptions;

namespace Nabadat.UserManagement.UnitTests.Permissions;

/// <summary>
/// T072 — write-first unit tests for <c>DataLayerAuthorizationGuard</c> (T079), the
/// single data-layer enforcement point services call before mutating permissions.
/// Enforcement lives here (not the controller), so it holds even when a caller
/// bypasses the API. On denial the guard emits a <c>permission.forbidden_attempt</c>
/// audit event to M-17 and throws <c>ForbiddenException</c>; the allow path is silent.
///
/// Authority split (FR-007): P-07 may assign only the non-CX modules (User
/// Management, Tenant Configuration); the 7 CX-domain modules are P-01-exclusive.
///
/// The guard type does not exist yet → this project fails to COMPILE, the valid red
/// state for a write-first story (CLAUDE.md Unit Test Policy, rule 7).
/// </summary>
public sealed class DataLayerAuthorizationGuardTests
{
    private const string SurveyBuilder = "SurveyBuilder";   // CX-domain — P-01 only
    private const string KpiConfiguration = "KpiConfiguration"; // CX-domain — P-01 only
    private const string UserManagement = "UserManagement"; // non-CX — P-07 allowed

    private readonly IUserManagementEventPublisher _events = Substitute.For<IUserManagementEventPublisher>();
    private readonly FakeTimeProvider _clock = new();
    private readonly Guid _actorId = Guid.NewGuid();

    private DataLayerAuthorizationGuard CreateSut() => new(_events, new FakeTenantDbContext(), _clock);

    [Theory]
    [InlineData(SurveyBuilder)]
    [InlineData(KpiConfiguration)]
    public async Task EnforceCanAssignModule_throws_forbidden_when_p07_targets_a_cx_domain_module(string moduleId)
    {
        var act = () => CreateSut().EnforceCanAssignModuleAsync(_actorId, "P-07", moduleId);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Theory]
    [InlineData("P-07", UserManagement)] // non-CX is allowed for the Tenant Administrator
    [InlineData("P-01", SurveyBuilder)]  // the CX Program Manager owns every CX module
    [InlineData("P-01", UserManagement)]
    public async Task EnforceCanAssignModule_allows_when_persona_is_permitted_for_module(string persona, string moduleId)
    {
        var act = () => CreateSut().EnforceCanAssignModuleAsync(_actorId, persona, moduleId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnforceCanAssignModule_publishes_forbidden_attempt_event_when_denied()
    {
        var act = () => CreateSut().EnforceCanAssignModuleAsync(_actorId, "P-07", SurveyBuilder);

        await act.Should().ThrowAsync<ForbiddenException>();
        await _events.Received().PublishAsync(
            Arg.Is<UserManagementEvent>(e => e.EventType == "permission.forbidden_attempt" && e.ActorId == _actorId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnforceCanAssignModule_publishes_no_event_when_allowed()
    {
        await CreateSut().EnforceCanAssignModuleAsync(_actorId, "P-07", UserManagement);

        await _events.DidNotReceive().PublishAsync(
            Arg.Any<UserManagementEvent>(),
            Arg.Any<CancellationToken>());
    }
}
