using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.UserManagement.Application.Auth;
using Nabadat.UserManagement.Application.Auth.Exceptions;
using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Permissions.Exceptions;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Users;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Infrastructure.Crypto;
using Nabadat.UserManagement.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace Nabadat.UserManagement.UnitTests.Users;

/// <summary>
/// T070 — write-first unit tests for <c>UserCreationPolicy</c> (T076). Only P-01
/// and P-07 may create tenant users (FR-007); the rule is enforced at the data
/// layer, so a non-privileged actor is rejected even when the controller is
/// bypassed. A created user is provisioned with its persona's baseline permission
/// modules, and a <c>user.created</c> event is co-written to M-17.
///
/// <c>UserCreationPolicy</c> does not exist yet → this project fails to COMPILE, the
/// valid red state for a write-first story (CLAUDE.md Unit Test Policy, rule 7).
/// </summary>
public sealed class UserCreationPolicyTests
{
    private const string NewUserPersona = "P-03"; // the persona being provisioned
    private const string BaselineModule = "SurveyBuilder";
    private const string ValidPassword = "ValidP@ss1";

    private readonly ITenantUserService _users = Substitute.For<ITenantUserService>();
    private readonly IPermissionModuleAssignmentService _permissions = Substitute.For<IPermissionModuleAssignmentService>();
    private readonly IPersonaBaselineService _baselines = Substitute.For<IPersonaBaselineService>();
    private readonly IUserManagementEventPublisher _events = Substitute.For<IUserManagementEventPublisher>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly FakeTimeProvider _clock = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    // Real validator (so weak-password rejection is exercised); mocked hasher.
    private UserCreationPolicy CreateSut() => new(
        _users, _permissions, _baselines, _events, new FakeTenantDbContext(), new PasswordValidator(), _hasher, _clock);

    [Theory]
    [InlineData("P-01")] // CX Program Manager
    [InlineData("P-07")] // Tenant Administrator
    public async Task CreateUser_persists_user_with_baseline_permissions_when_actor_is_privileged(string actorPersona)
    {
        _baselines.GetAsync(_tenantId, NewUserPersona, Arg.Any<CancellationToken>())
            .Returns(new PersonaBaseline
            {
                BaselineId = Guid.NewGuid(),
                TenantId = _tenantId,
                PersonaId = NewUserPersona,
                PermissionModuleAssignments = [new PersonaModuleAssignment { ModuleId = BaselineModule, AllowedModes = ["View", "Manage"] }],
            });
        _hasher.Hash(Arg.Any<string>()).Returns("hashed-pw");

        await CreateSut().CreateUserAsync(_tenantId, _actorId, actorPersona, "new.user@example.com", NewUserPersona, ValidPassword);

        // The admin-set password is hashed and stored, so the new user can sign in (I-01).
        await _users.Received().AddAsync(
            Arg.Is<TenantUser>(u => u.Username == "new.user@example.com" && u.Persona == NewUserPersona && u.PasswordHash == "hashed-pw"),
            Arg.Any<CancellationToken>());
        await _permissions.Received().ReplaceAssignmentsAsync(
            Arg.Any<Guid>(),
            Arg.Is<IReadOnlyList<PermissionModuleAssignment>>(a => a.Any(x => x.ModuleId == BaselineModule)),
            Arg.Any<CancellationToken>());
        await _events.Received().PublishAsync(
            Arg.Is<UserManagementEvent>(e => e.EventType == "user.created"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("P-02")]
    [InlineData("P-08")]
    public async Task CreateUser_throws_forbidden_and_creates_nothing_when_actor_is_not_privileged(string actorPersona)
    {
        var act = () => CreateSut().CreateUserAsync(_tenantId, _actorId, actorPersona, "blocked@example.com", NewUserPersona, ValidPassword);

        await act.Should().ThrowAsync<ForbiddenException>();
        await _users.DidNotReceive().AddAsync(
            Arg.Any<TenantUser>(),
            Arg.Any<CancellationToken>());
    }

    // A privileged actor whose initial password fails complexity is rejected before any write.
    [Fact]
    public async Task CreateUser_throws_weak_password_and_creates_nothing_when_password_too_simple()
    {
        var act = () => CreateSut().CreateUserAsync(_tenantId, _actorId, "P-01", "weak@example.com", NewUserPersona, "short");

        await act.Should().ThrowAsync<WeakPasswordException>();
        await _users.DidNotReceive().AddAsync(Arg.Any<TenantUser>(), Arg.Any<CancellationToken>());
    }
}
