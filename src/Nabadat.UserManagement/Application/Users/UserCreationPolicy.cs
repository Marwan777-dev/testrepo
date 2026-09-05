using Nabadat.UserManagement.Application.Auth.Exceptions;
using Nabadat.UserManagement.Application.Auth.Interfaces;
using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Permissions.Exceptions;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Infrastructure.Crypto;

namespace Nabadat.UserManagement.Application.Users;

/// <summary>
/// Creates a tenant user and provisions it from its persona's authorization-matrix
/// baseline (FR-007). Only P-01 (CX Program Manager) and P-07 (Tenant Administrator)
/// may create users; any other persona is rejected with <see cref="ForbiddenException"/>
/// at the data layer — before any row is written — so the rule holds even if the
/// controller is bypassed. The new user (status <c>pending-enrollment</c>), its
/// baseline module assignments, and the <c>user.created</c> audit event all commit in
/// one transaction (FR-015).
/// </summary>
public sealed class UserCreationPolicy
{
    private static readonly IReadOnlySet<string> UserCreatingPersonas =
        new HashSet<string> { "P-01", "P-07" };

    private readonly ITenantUserService _users;
    private readonly IPermissionModuleAssignmentService _permissions;
    private readonly IPersonaBaselineService _baselines;
    private readonly IUserManagementEventPublisher _events;
    private readonly ITenantDbContext _context;
    private readonly IPasswordValidator _passwordValidator;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _clock;

    public UserCreationPolicy(
        ITenantUserService users,
        IPermissionModuleAssignmentService permissions,
        IPersonaBaselineService baselines,
        IUserManagementEventPublisher events,
        ITenantDbContext context,
        IPasswordValidator passwordValidator,
        IPasswordHasher passwordHasher,
        TimeProvider clock)
    {
        _users = users;
        _permissions = permissions;
        _baselines = baselines;
        _events = events;
        _context = context;
        _passwordValidator = passwordValidator;
        _passwordHasher = passwordHasher;
        _clock = clock;
    }

    /// <summary>
    /// Data-layer authority check shared by every user-management action (FR-007):
    /// throws <see cref="ForbiddenException"/> unless the actor is P-01 or P-07.
    /// <c>UserManagementService</c> calls this at the top of each lifecycle method so
    /// the rule is enforced even when the controller is bypassed.
    /// </summary>
    public void EnsureCanManageUsers(string actorPersona)
    {
        if (!UserCreatingPersonas.Contains(actorPersona))
        {
            throw new ForbiddenException(
                $"Persona {actorPersona} may not manage tenant users; only P-01 and P-07 may.");
        }
    }

    /// <summary>
    /// Data-layer authority check for reading tenant users (FR-007,
    /// <c>UserManagement.View</c>): throws <see cref="ForbiddenException"/> unless the
    /// actor is P-01 or P-07 — the only personas the contract grants the user directory
    /// to. Gates the list and user-detail read paths so the rule holds at the service
    /// layer, not merely the UI.
    /// </summary>
    public void EnsureCanViewUsers(string actorPersona)
    {
        if (!UserCreatingPersonas.Contains(actorPersona))
        {
            throw new ForbiddenException(
                $"Persona {actorPersona} may not view tenant users; only P-01 and P-07 may.");
        }
    }

    /// <summary>
    /// Validates the actor persona, then creates <paramref name="newUsername"/> with the
    /// admin-set <paramref name="password"/> (FR-027 complexity) and the
    /// <paramref name="newUserPersona"/> baseline applied. The new user can sign in with
    /// the password and enrols MFA on first login (status <c>pending-enrollment</c>;
    /// resolves gap I-01). Throws <see cref="ForbiddenException"/> if the actor is not
    /// P-01/P-07, or <see cref="WeakPasswordException"/> if the password fails complexity —
    /// both before any row is written.
    /// </summary>
    public async Task<TenantUser> CreateUserAsync(
        Guid tenantId,
        Guid actorId,
        string actorPersona,
        string newUsername,
        string newUserPersona,
        string password,
        CancellationToken ct = default)
    {
        EnsureCanManageUsers(actorPersona);

        var validation = _passwordValidator.ValidatePassword(password);
        if (!validation.IsValid)
        {
            throw new WeakPasswordException(validation.Errors);
        }

        var baseline = await _baselines.GetAsync(tenantId, newUserPersona, ct);
        var now = _clock.GetUtcNow();

        var user = new TenantUser
        {
            UserId = Guid.NewGuid(),
            Username = newUsername,
            PasswordHash = _passwordHasher.Hash(password),
            Persona = newUserPersona,
            Status = UserStatus.PendingEnrollment,
            IsMfaEnrolled = false,
            LastPermissionSnapshotVersion = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var assignments = (baseline?.PermissionModuleAssignments ?? [])
            .Select(m => new PermissionModuleAssignment
            {
                AssignmentId = Guid.NewGuid(),
                UserId = user.UserId,
                ModuleId = m.ModuleId,
                AllowedModes = m.AllowedModes,
                AssignedBy = actorId,
                CreatedAt = now,
                UpdatedAt = now,
            })
            .ToList();

        await _context.ExecuteAsync(async () =>
        {
            await _users.AddAsync(user, ct);
            await _permissions.ReplaceAssignmentsAsync(user.UserId, assignments, ct);
            await _events.PublishAsync(new UserManagementEvent
            {
                EventType = "user.created",
                ActorId = actorId,
                ActorPersona = actorPersona,
                EntityType = nameof(TenantUser),
                EntityId = user.UserId,
                NewValue = new { user.Username, user.Persona, Status = user.Status.ToWire() },
                OccurredAtUtc = now,
                CorrelationId = Guid.NewGuid(),
            }, ct);
        }, ct);

        return user;
    }
}
