using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Users;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.Interfaces;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Application.Auth;
using Nabadat.UserManagement.Infrastructure.Crypto;
using Nabadat.UserManagement.UnitTests.TestSupport;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Auth.Interfaces;

namespace Nabadat.UserManagement.UnitTests.Events;

/// <summary>
/// T113 — write-first unit tests for FR-015's atomic audit-write contract: every
/// state-mutating M-10 action co-writes its M-17 event <i>in the same transaction</i>
/// as the entity change, so the two commit or roll back together. Drives the
/// orchestration of <c>UserManagementService</c> over <c>ITenantDbContext.ExecuteAsync</c> and
/// <c>IUserManagementEventWriter</c> (T116).
///
/// What the unit lane proves: (1) the entity write and the event publish are issued
/// inside a <i>single</i> unit of work; (2) when the publish throws, the action
/// surfaces the failure and the unit of work never reaches commit — modelling the real
/// transaction's rollback; (3) exactly one event is published per action, with the
/// correct type and payload. The real Postgres commit/rollback semantics are verified
/// in the integration lane (AuditTransactionIntegrationTests, T121).
/// </summary>
public sealed class AuditTransactionTests
{
    private const string P01 = "P-01";

    private readonly ITenantUserService _users = Substitute.For<ITenantUserService>();
    private readonly IPermissionModuleAssignmentService _permissions = Substitute.For<IPermissionModuleAssignmentService>();
    private readonly IPersonaBaselineService _baselines = Substitute.For<IPersonaBaselineService>();
    private readonly IAuthSessionService _sessions = Substitute.For<IAuthSessionService>();
    private readonly IPasswordResetTokenService _resetTokens = Substitute.For<IPasswordResetTokenService>();
    private readonly IM09NotificationService _notifications = Substitute.For<IM09NotificationService>();
    private readonly IUserManagementEventPublisher _events = Substitute.For<IUserManagementEventPublisher>();
    private readonly FakeTimeProvider _clock = new();

    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _targetUserId = Guid.NewGuid();

    private UserManagementService CreateSut(RecordingTenantDbContext uow)
    {
        var policy = new UserCreationPolicy(_users, _permissions, _baselines, _events, uow, new PasswordValidator(), Substitute.For<IPasswordHasher>(), _clock);
        return new UserManagementService(
            policy, _users, _sessions, _resetTokens, _notifications, _events, uow, _clock);
    }

    private TenantUser ExistingUser() => new()
    {
        UserId = _targetUserId,
        Username = "alice@example.com",
        Persona = "P-03",
        Status = UserStatus.Active,
        LastPermissionSnapshotVersion = 1,
    };

    // ── Case 1: entity change + event publish commit atomically in one transaction ──
    [Fact]
    public async Task UpdateProfile_commits_entity_change_and_event_in_a_single_transaction()
    {
        _users.GetByIdAsync(_targetUserId, Arg.Any<CancellationToken>()).Returns(ExistingUser());
        var uow = new RecordingTenantDbContext();

        await CreateSut(uow).UpdateProfileAsync(_actorId, P01, _targetUserId, newPersona: null, newOrganizationNodeId: Guid.NewGuid());

        // Both writes are issued inside exactly one unit of work, which then commits.
        uow.ExecuteCount.Should().Be(1);
        uow.Committed.Should().BeTrue();
        await _users.Received(1).UpdateAsync(
            Arg.Is<TenantUser>(u => u.UserId == _targetUserId),
            Arg.Any<CancellationToken>());
        await _events.Received(1).PublishAsync(
            Arg.Is<UserManagementEvent>(e => e.EventType == "user.updated"),
            Arg.Any<CancellationToken>());
    }

    // ── Case 2: a failed M-17 publish rolls back the whole transaction (no commit) ──
    [Fact]
    public async Task UpdateProfile_rolls_back_entire_transaction_when_event_publish_fails()
    {
        _users.GetByIdAsync(_targetUserId, Arg.Any<CancellationToken>()).Returns(ExistingUser());
        _events.PublishAsync(Arg.Any<UserManagementEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("event_log write failed"));
        var uow = new RecordingTenantDbContext();

        var act = () => CreateSut(uow).UpdateProfileAsync(_actorId, P01, _targetUserId, newPersona: null, newOrganizationNodeId: null);

        // The failure surfaces and the unit of work never reaches commit → the entity
        // change rolls back alongside the event (FR-015 atomicity).
        await act.Should().ThrowAsync<InvalidOperationException>();
        uow.Committed.Should().BeFalse();
    }

    // ── Case 3: exactly one event per auditable action, with the right type + payload ──
    [Fact]
    public async Task Deactivate_publishes_exactly_one_event_with_correct_type_and_payload()
    {
        _users.GetByIdAsync(_targetUserId, Arg.Any<CancellationToken>()).Returns(ExistingUser());
        var uow = new RecordingTenantDbContext();

        await CreateSut(uow).DeactivateUserAsync(_actorId, P01, _targetUserId);

        await _events.Received(1).PublishAsync(
            Arg.Is<UserManagementEvent>(e =>
                e.EventType == "user.deactivated"
                && e.ActorId == _actorId
                && e.ActorPersona == P01
                && e.EntityType == nameof(TenantUser)
                && e.EntityId == _targetUserId),
            Arg.Any<CancellationToken>());
    }
}
