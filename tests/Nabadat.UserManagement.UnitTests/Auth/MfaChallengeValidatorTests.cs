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
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Infrastructure.Crypto;
using Nabadat.UserManagement.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace Nabadat.UserManagement.UnitTests.Auth;

public sealed class MfaChallengeValidatorTests
{
    private readonly IMfaChallengeService _challenges = Substitute.For<IMfaChallengeService>();
    private readonly ITenantUserService _users = Substitute.For<ITenantUserService>();
    private readonly ITotpService _totp = Substitute.For<ITotpService>();
    private readonly IMfaSecretEncryptionService _encryption = Substitute.For<IMfaSecretEncryptionService>();
    private readonly ISessionService _sessions = Substitute.For<ISessionService>();
    private readonly IAccountLockout _lockout = Substitute.For<IAccountLockout>();
    private readonly IUserManagementEventPublisher _events = Substitute.For<IUserManagementEventPublisher>();
    private readonly FakeTimeProvider _clock = new();

    private MfaChallengeValidator CreateSut() =>
        new(_challenges, _users, _totp, _encryption, _sessions, _lockout, _events, new FakeTenantDbContext(), _clock);

    [Fact]
    public async Task VerifyAsync_creates_session_and_emits_succeeded_event_when_code_valid()
    {
        var user = EnrolledUser();
        ArrangeChallenge(user);
        _totp.VerifyCode("DECRYPTED-SECRET", "123456", user.LastUsedTotpStep)
            .Returns(new TotpVerificationResult { IsValid = true, MatchedStep = 42 });
        _sessions.CreateSessionAsync(user.UserId, Arg.Any<CancellationToken>())
            .Returns(new SessionCreationResult { RawToken = "nbd_token", Session = NewSession() });

        var result = await CreateSut().VerifyAsync("challenge-1", "123456");

        result.SessionToken.Should().Be("nbd_token");
        await _events.Received().PublishAsync(
            Arg.Is<UserManagementEvent>(e => e.EventType == "authentication.mfa.succeeded"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyAsync_throws_and_emits_failed_event_when_code_invalid()
    {
        var user = EnrolledUser();
        ArrangeChallenge(user);
        _totp.VerifyCode("DECRYPTED-SECRET", "000000", user.LastUsedTotpStep)
            .Returns(TotpVerificationResult.Invalid());

        var act = () => CreateSut().VerifyAsync("challenge-1", "000000");

        await act.Should().ThrowAsync<MfaValidationException>();
        await _events.Received().PublishAsync(
            Arg.Is<UserManagementEvent>(e => e.EventType == "authentication.mfa.failed"),
            Arg.Any<CancellationToken>());
        await _lockout.Received().RecordFailedAttemptAsync(user.UserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyAsync_rejects_replayed_code_at_same_step()
    {
        var user = EnrolledUser();
        user.LastUsedTotpStep = 42;
        ArrangeChallenge(user);
        // A replayed code resolves to a step that is not after the last accepted one → invalid.
        _totp.VerifyCode("DECRYPTED-SECRET", "123456", 42).Returns(TotpVerificationResult.Invalid());

        var act = () => CreateSut().VerifyAsync("challenge-1", "123456");

        await act.Should().ThrowAsync<MfaValidationException>();
    }

    private void ArrangeChallenge(TenantUser user)
    {
        _challenges.ResolveChallenge("challenge-1")
            .Returns(new MfaChallenge { UserId = user.UserId, RequiresEnrollment = false });
        _users.GetByIdAsync(user.UserId, Arg.Any<CancellationToken>()).Returns(user);
        _encryption.DecryptAsync(user.MfaSecretEncrypted!, user.MfaSecretKeyRef!, Arg.Any<CancellationToken>())
            .Returns("DECRYPTED-SECRET");
    }

    private static TenantUser EnrolledUser() => new()
    {
        UserId = Guid.NewGuid(),
        Username = "alice@example.com",
        Persona = "P-01",
        Status = UserStatus.Active,
        IsMfaEnrolled = true,
        MfaSecretEncrypted = [1, 2, 3],
        MfaSecretKeyRef = "key-ref",
        LastUsedTotpStep = null,
    };

    private AuthSession NewSession() => new()
    {
        SessionId = Guid.NewGuid(),
        AbsoluteExpiresAtUtc = _clock.GetUtcNow().AddHours(24),
        PermissionSnapshot = new PermissionSnapshot(),
    };
}
