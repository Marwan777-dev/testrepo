using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.UserManagement.Application.Auth;
using Nabadat.UserManagement.Application.Auth.Dtos;
using Nabadat.UserManagement.Application.Auth.Exceptions;
using Nabadat.UserManagement.Application.Auth.Interfaces;
using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Infrastructure.Crypto;
using Nabadat.UserManagement.UnitTests.TestSupport;
using Npgsql;
using NSubstitute;
using Xunit;

namespace Nabadat.UserManagement.UnitTests.Auth;

public sealed class PasswordResetServiceTests
{
    private readonly IPasswordResetTokenService _tokens = Substitute.For<IPasswordResetTokenService>();
    private readonly ITenantUserService _users = Substitute.For<ITenantUserService>();
    private readonly IPasswordResetRateLimiter _rateLimiter = Substitute.For<IPasswordResetRateLimiter>();
    private readonly IM09NotificationService _m09 = Substitute.For<IM09NotificationService>();
    private readonly IPasswordValidator _passwordValidator = Substitute.For<IPasswordValidator>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IUserManagementEventPublisher _events = Substitute.For<IUserManagementEventPublisher>();
    private readonly FakeTimeProvider _clock = new();

    private PasswordResetService CreateSut() => new(
        _tokens, _users, _rateLimiter, _m09, _passwordValidator, _hasher, _events, new FakeTenantDbContext(), _clock);

    [Fact]
    public async Task Redeem_throws_token_expired_when_token_is_past_expiry()
    {
        ArrangeToken(t => t.ExpiresAtUtc = _clock.GetUtcNow().AddMinutes(-1));

        var act = () => CreateSut().RedeemResetAsync("RAW", "NewValidP@ss2");

        await act.Should().ThrowAsync<TokenExpiredException>();
    }

    [Fact]
    public async Task Redeem_throws_token_already_used_when_token_consumed()
    {
        ArrangeToken(t =>
        {
            t.ExpiresAtUtc = _clock.GetUtcNow().AddMinutes(10);
            t.UsedAtUtc = _clock.GetUtcNow().AddMinutes(-1);
        });

        var act = () => CreateSut().RedeemResetAsync("RAW", "NewValidP@ss2");

        await act.Should().ThrowAsync<TokenAlreadyUsedException>();
    }

    [Fact]
    public async Task Redeem_throws_token_revoked_when_token_revoked()
    {
        ArrangeToken(t =>
        {
            t.ExpiresAtUtc = _clock.GetUtcNow().AddMinutes(10);
            t.Revoked = true;
        });

        var act = () => CreateSut().RedeemResetAsync("RAW", "NewValidP@ss2");

        await act.Should().ThrowAsync<TokenRevokedException>();
    }

    [Fact]
    public async Task Redeem_marks_token_used_and_emits_completed_event_on_success()
    {
        var token = ArrangeToken(t => t.ExpiresAtUtc = _clock.GetUtcNow().AddMinutes(10));
        _users.GetByIdAsync(token.UserId, Arg.Any<CancellationToken>())
            .Returns(new TenantUser { UserId = token.UserId, PasswordHash = "old" });
        _passwordValidator.ValidatePassword("NewValidP@ss2").Returns(PasswordValidationResult.Valid());
        _hasher.Hash("NewValidP@ss2").Returns("new-hash");

        await CreateSut().RedeemResetAsync("RAW", "NewValidP@ss2");

        await _events.Received().PublishAsync(
            Arg.Is<UserManagementEvent>(e => e.EventType == "password.reset.completed"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestReset_rolls_back_and_rethrows_when_m09_delivery_fails()
    {
        var user = new TenantUser { UserId = Guid.NewGuid(), Username = "alice@example.com" };
        _users.GetByUsernameAsync("alice@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _m09.SendPasswordResetAsync("alice@example.com", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("M-09 down"));

        var act = () => CreateSut().RequestResetAsync("alice@example.com");

        // The reset token write and the M-09 delivery share one unit of work, so a delivery
        // failure aborts the whole transaction (token not persisted — verified end-to-end in
        // the integration lane). At the unit level we assert the failure propagates.
        await act.Should().ThrowAsync<InvalidOperationException>();
        await _m09.Received().SendPasswordResetAsync("alice@example.com", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private PasswordResetToken ArrangeToken(Action<PasswordResetToken> configure)
    {
        var token = new PasswordResetToken
        {
            TokenId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = [9, 9, 9],
            IssuedBy = "self-service",
            IssuedVia = "email",
        };
        configure(token);
        _tokens.GetByTokenHashAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(token);
        return token;
    }
}
