using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.UserManagement.Application.Auth;
using Nabadat.UserManagement.Application.Auth.Dtos;
using Nabadat.UserManagement.Application.Auth.Interfaces;
using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace Nabadat.UserManagement.UnitTests.Auth;

public sealed class SessionServiceTests
{
    private readonly ITenantUserService _users = Substitute.For<ITenantUserService>();
    private readonly IAuthSessionService _sessions = Substitute.For<IAuthSessionService>();
    private readonly IPermissionModuleAssignmentService _permissions = Substitute.For<IPermissionModuleAssignmentService>();
    private readonly IUserManagementEventPublisher _events = Substitute.For<IUserManagementEventPublisher>();
    private readonly FakeTimeProvider _clock = new();

    private SessionService CreateSut() =>
        new(_users, _sessions, _permissions, _events, new FakeTenantDbContext(), _clock);

    [Fact]
    public async Task CreateSession_builds_snapshot_at_users_current_permission_version()
    {
        var user = NewUser(snapshotVersion: 0);
        _users.GetByIdAsync(user.UserId, Arg.Any<CancellationToken>()).Returns(user);
        _permissions.GetAssignmentsAsync(user.UserId, Arg.Any<CancellationToken>()).Returns([]);

        var result = await CreateSut().CreateSessionAsync(user.UserId);

        result.Session.PermissionSnapshot.Version.Should().Be(0);
        result.RawToken.Should().StartWith("nbd_");
    }

    [Fact]
    public async Task CreateSession_rebuilds_snapshot_at_bumped_version_after_permission_change()
    {
        var user = NewUser(snapshotVersion: 7);
        _users.GetByIdAsync(user.UserId, Arg.Any<CancellationToken>()).Returns(user);
        _permissions.GetAssignmentsAsync(user.UserId, Arg.Any<CancellationToken>()).Returns([]);

        var result = await CreateSut().CreateSessionAsync(user.UserId);

        result.Session.PermissionSnapshot.Version.Should().Be(7);
    }

    [Fact]
    public async Task ValidateSession_returns_null_when_absolute_expiry_elapsed()
    {
        var session = NewSession(version: 0);
        session.AbsoluteExpiresAtUtc = _clock.GetUtcNow().AddMinutes(-1);
        _sessions.GetByTokenHashAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(session);

        var result = await CreateSut().ValidateSessionAsync("nbd_token");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateSession_resets_sliding_activity_and_returns_context_when_valid()
    {
        var user = NewUser(snapshotVersion: 0);
        var session = NewSession(version: 0);
        session.UserId = user.UserId;
        session.AbsoluteExpiresAtUtc = _clock.GetUtcNow().AddHours(24);
        session.LastActivityAtUtc = _clock.GetUtcNow().AddMinutes(-10);
        _sessions.GetByTokenHashAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(session);
        _users.GetByIdAsync(user.UserId, Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateSut().ValidateSessionAsync("nbd_token");

        result.Should().NotBeNull();
        result!.UserId.Should().Be(user.UserId);
        await _sessions.Received().UpdateActivityAsync(session.SessionId, _clock.GetUtcNow(), Arg.Any<CancellationToken>());
    }

    private TenantUser NewUser(long snapshotVersion) => new()
    {
        UserId = Guid.NewGuid(),
        Username = "alice@example.com",
        Persona = "P-01",
        Status = UserStatus.Active,
        LastPermissionSnapshotVersion = snapshotVersion,
    };

    private AuthSession NewSession(long version) => new()
    {
        SessionId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        TokenHash = [1, 2, 3],
        IssuedAtUtc = _clock.GetUtcNow(),
        AbsoluteExpiresAtUtc = _clock.GetUtcNow().AddHours(24),
        LastActivityAtUtc = _clock.GetUtcNow(),
        SlidingTtlMinutes = 60,
        PermissionSnapshotVersion = version,
        PermissionSnapshot = new PermissionSnapshot { Version = version },
        IsActive = true,
        CreatedAt = _clock.GetUtcNow(),
    };
}
