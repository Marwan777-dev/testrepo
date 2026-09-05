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

public sealed class TenantAuthenticationServiceTests
{
    private readonly ITenantUserService _users = Substitute.For<ITenantUserService>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IMfaChallengeService _challenges = Substitute.For<IMfaChallengeService>();
    private readonly IUserManagementEventPublisher _events = Substitute.For<IUserManagementEventPublisher>();
    private readonly FakeTimeProvider _clock = new();

    private TenantAuthenticationService CreateSut() =>
        new(_users, _hasher, _challenges, _events, new FakeTenantDbContext(), _clock);

    [Fact]
    public async Task CreateUser_returns_invalid_email_when_username_is_not_an_email()
    {
        var result = await CreateSut().CreateUserAsync("not-an-email", "ValidP@ss1", "P-01");

        result.Outcome.Should().Be(CreateUserOutcome.InvalidEmail);
    }

    [Fact]
    public async Task CreateUser_returns_conflict_when_username_already_exists()
    {
        _users.ExistsAsync("alice@example.com", Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateSut().CreateUserAsync("alice@example.com", "ValidP@ss1", "P-01");

        result.Outcome.Should().Be(CreateUserOutcome.Conflict);
    }

    [Fact]
    public async Task ValidateCredentials_issues_challenge_when_password_valid_and_mfa_enrolled()
    {
        var user = NewUser(isMfaEnrolled: true);
        _users.GetByUsernameAsync("alice@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("CorrectPassword", user.PasswordHash).Returns(true);
        _challenges.CreateChallenge(user.UserId, false).Returns("challenge-1");

        var result = await CreateSut().ValidateCredentialsAsync("alice@example.com", "CorrectPassword");

        result.Outcome.Should().Be(CredentialOutcome.ChallengeIssued);
        result.ChallengeId.Should().Be("challenge-1");
    }

    [Fact]
    public async Task ValidateCredentials_requires_enrollment_when_user_not_mfa_enrolled()
    {
        var user = NewUser(isMfaEnrolled: false);
        _users.GetByUsernameAsync("bob@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("CorrectPassword", user.PasswordHash).Returns(true);
        _challenges.CreateChallenge(user.UserId, true).Returns("challenge-2");

        var result = await CreateSut().ValidateCredentialsAsync("bob@example.com", "CorrectPassword");

        result.Outcome.Should().Be(CredentialOutcome.RequiresMfaEnrollment);
        result.ChallengeId.Should().Be("challenge-2");
    }

    [Fact]
    public async Task ValidateCredentials_throws_account_locked_when_locked_and_cooldown_not_elapsed()
    {
        var user = NewUser(isMfaEnrolled: true);
        user.Status = UserStatus.Locked;
        user.LockedUntilUtc = _clock.GetUtcNow().AddMinutes(5);
        _users.GetByUsernameAsync("alice@example.com", Arg.Any<CancellationToken>()).Returns(user);

        var act = () => CreateSut().ValidateCredentialsAsync("alice@example.com", "CorrectPassword");

        await act.Should().ThrowAsync<AccountLockedException>();
    }

    private TenantUser NewUser(bool isMfaEnrolled) => new()
    {
        UserId = Guid.NewGuid(),
        Username = "user@example.com",
        PasswordHash = "stored-hash",
        Persona = "P-01",
        Status = UserStatus.Active,
        IsMfaEnrolled = isMfaEnrolled,
    };
}
