using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.UserManagement.Application.Auth;
using Nabadat.UserManagement.Application.Auth.Interfaces;
using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace Nabadat.UserManagement.UnitTests.Auth;

public sealed class AccountLockoutTests
{
    private readonly ITenantUserService _users = Substitute.For<ITenantUserService>();
    private readonly IUserManagementEventPublisher _events = Substitute.For<IUserManagementEventPublisher>();
    private readonly FakeTimeProvider _clock = new();

    private AccountLockoutService CreateSut() =>
        new(_users, _events, new FakeTenantDbContext(), _clock);

    [Fact]
    public async Task RecordFailedAttempt_locks_account_and_emits_event_on_fifth_failure()
    {
        var user = new TenantUser { UserId = Guid.NewGuid(), Status = UserStatus.Active, FailedAttemptCount = 4 };
        _users.GetByIdAsync(user.UserId, Arg.Any<CancellationToken>()).Returns(user);

        await CreateSut().RecordFailedAttemptAsync(user.UserId);

        user.Status.Should().Be(UserStatus.Locked);
        user.LockedUntilUtc.Should().Be(_clock.GetUtcNow().AddMinutes(15));
        await _events.Received().PublishAsync(
            Arg.Is<UserManagementEvent>(e => e.EventType == "authentication.account.locked"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AutoUnlockIfExpired_unlocks_and_emits_event_after_cooldown()
    {
        var user = new TenantUser
        {
            UserId = Guid.NewGuid(),
            Status = UserStatus.Locked,
            FailedAttemptCount = 5,
            LockedUntilUtc = _clock.GetUtcNow().AddMinutes(15),
        };
        _users.GetByIdAsync(user.UserId, Arg.Any<CancellationToken>()).Returns(user);
        _clock.Advance(TimeSpan.FromMinutes(16));

        var unlocked = await CreateSut().AutoUnlockIfExpiredAsync(user.UserId);

        unlocked.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Active);
        user.FailedAttemptCount.Should().Be(0);
        await _events.Received().PublishAsync(
            Arg.Is<UserManagementEvent>(e => e.EventType == "authentication.account.unlocked"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AutoUnlockIfExpired_returns_false_when_cooldown_not_elapsed()
    {
        var user = new TenantUser
        {
            UserId = Guid.NewGuid(),
            Status = UserStatus.Locked,
            LockedUntilUtc = _clock.GetUtcNow().AddMinutes(15),
        };
        _users.GetByIdAsync(user.UserId, Arg.Any<CancellationToken>()).Returns(user);

        var unlocked = await CreateSut().AutoUnlockIfExpiredAsync(user.UserId);

        unlocked.Should().BeFalse();
        user.Status.Should().Be(UserStatus.Locked);
    }

    [Fact]
    public async Task Unlock_clears_lock_and_emits_event_immediately()
    {
        var user = new TenantUser
        {
            UserId = Guid.NewGuid(),
            Status = UserStatus.Locked,
            FailedAttemptCount = 5,
            LockedUntilUtc = _clock.GetUtcNow().AddMinutes(15),
        };
        _users.GetByIdAsync(user.UserId, Arg.Any<CancellationToken>()).Returns(user);

        await CreateSut().UnlockAsync(user.UserId);

        user.Status.Should().Be(UserStatus.Active);
        await _events.Received().PublishAsync(
            Arg.Is<UserManagementEvent>(e => e.EventType == "authentication.account.unlocked"),
            Arg.Any<CancellationToken>());
    }
}
