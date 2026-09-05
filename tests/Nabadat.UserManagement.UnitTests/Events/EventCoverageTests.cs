using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.UserManagement.Application.Auth;
using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Permissions;
using Nabadat.UserManagement.Application.Users;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Infrastructure.Crypto;
using Nabadat.UserManagement.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Application.Auth.Interfaces;

namespace Nabadat.UserManagement.UnitTests.Events;

/// <summary>
/// T114 — write-first unit tests asserting audit-event <i>coverage</i>: every
/// state-mutating M-10 service path emits the canonical M-17 event for its action
/// (FR-015, T116). One representative per event family is verified here via an
/// <c>NSubstitute</c> mock of <see cref="IUserManagementEventPublisher"/>:
/// <c>user.deactivated</c> (<see cref="UserManagementService.DeactivateUserAsync"/>),
/// <c>session.revoked</c> (<see cref="SessionService.InvalidateSessionAsync"/>), and
/// <c>permission.modified</c> (<see cref="PermissionAssignmentService.ReplacePermissionsAsync"/>).
/// A mutation that ships no event is a coverage gap and fails here.
/// </summary>
public sealed class EventCoverageTests
{
    private const string P01 = "P-01";

    private readonly ITenantUserService _users = Substitute.For<ITenantUserService>();
    private readonly IPermissionModuleAssignmentService _permissions = Substitute.For<IPermissionModuleAssignmentService>();
    private readonly IPersonaBaselineService _baselines = Substitute.For<IPersonaBaselineService>();
    private readonly IAuthSessionService _sessions = Substitute.For<IAuthSessionService>();
    private readonly IPasswordResetTokenService _resetTokens = Substitute.For<IPasswordResetTokenService>();
    private readonly IM09NotificationService _notifications = Substitute.For<IM09NotificationService>();
    private readonly IUserManagementEventPublisher _events = Substitute.For<IUserManagementEventPublisher>();
    // EF data-access mocks for already-converted services (SessionService).
    private readonly ITenantUserService _userService = Substitute.For<ITenantUserService>();
    private readonly IAuthSessionService _sessionService = Substitute.For<IAuthSessionService>();
    private readonly IPermissionModuleAssignmentService _permissionService = Substitute.For<IPermissionModuleAssignmentService>();
    private readonly IUserManagementEventPublisher _eventWriter = Substitute.For<IUserManagementEventPublisher>();
    private readonly FakeTimeProvider _clock = new();

    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();

    private TenantUser ExistingUser() => new()
    {
        UserId = _targetUserId,
        Username = "alice@example.com",
        Persona = "P-03",
        Status = UserStatus.Active,
        LastPermissionSnapshotVersion = 1,
    };

    // ── user.deactivated ──
    [Fact]
    public async Task DeactivateUser_publishes_user_deactivated_event()
    {
        _users.GetByIdAsync(_targetUserId, Arg.Any<CancellationToken>()).Returns(ExistingUser());
        var policy = new UserCreationPolicy(_users, _permissions, _baselines, _events, new FakeTenantDbContext(), new PasswordValidator(), Substitute.For<IPasswordHasher>(), _clock);
        var sut = new UserManagementService(
            policy, _users, _sessions, _resetTokens, _notifications, _events, new FakeTenantDbContext(), _clock);

        await sut.DeactivateUserAsync(_actorId, P01, _targetUserId);

        await _events.Received(1).PublishAsync(
            Arg.Is<UserManagementEvent>(e => e.EventType == "user.deactivated" && e.EntityId == _targetUserId),
            Arg.Any<CancellationToken>());
    }

    // ── session.revoked ──
    [Fact]
    public async Task InvalidateSession_publishes_session_revoked_event()
    {
        var sessionId = Guid.NewGuid();
        var sut = new SessionService(_userService, _sessionService, _permissionService, _eventWriter, new FakeTenantDbContext(), _clock);

        await sut.InvalidateSessionAsync(sessionId);

        await _eventWriter.Received(1).PublishAsync(
            Arg.Is<UserManagementEvent>(e => e.EventType == "session.revoked" && e.EntityId == sessionId),
            Arg.Any<CancellationToken>());
    }

    // ── permission.modified ──
    [Fact]
    public async Task ReplacePermissions_publishes_permission_modified_event()
    {
        _users.GetByIdAsync(_targetUserId, Arg.Any<CancellationToken>()).Returns(ExistingUser());
        var guard = new DataLayerAuthorizationGuard(_events, new FakeTenantDbContext(), _clock);
        var sut = new PermissionAssignmentService(
            _permissions, _users, guard, _events, new FakeTenantDbContext(), _clock);

        // Revoke-all (empty target set) is an auditable permission change.
        await sut.ReplacePermissionsAsync(_actorId, P01, _targetUserId, []);

        await _events.Received(1).PublishAsync(
            Arg.Is<UserManagementEvent>(e => e.EventType == "permission.modified" && e.EntityId == _targetUserId),
            Arg.Any<CancellationToken>());
    }
}
