using Microsoft.EntityFrameworkCore;
using Nabadat.UserManagement.Application.Events.Dtos;
using Nabadat.UserManagement.Application.Events.Interfaces;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Application.Permissions.Interfaces;
using Nabadat.UserManagement.Application.Users.Interfaces;
using Nabadat.UserManagement.Domain.Entities;

namespace Nabadat.UserManagement.Application.Permissions;

/// <summary>
/// Per-user custom authorization rules (US3, T106). This one class is both the EF
/// data-access over <c>custom_authorization_rules</c> (implementing
/// <see cref="ICustomAuthorizationRuleService"/>, consumed by the read paths in
/// <c>PermissionEvaluationService</c> and <c>DataScopeController</c>) and the
/// create/update/delete use cases. Each mutation bumps the target user's
/// <c>LastPermissionSnapshotVersion</c> so the new fine-grained grants take effect on the
/// next session refresh (FR-013), and co-writes a
/// <c>custom_rule.created</c>/<c>updated</c>/<c>deleted</c> audit event in the same
/// transaction (FR-015). Actor authority (P-01/P-07) is enforced at the API boundary.
/// </summary>
public sealed class CustomAuthorizationRuleService : ICustomAuthorizationRuleService
{
    private readonly ITenantUserService _users;
    private readonly IUserManagementEventPublisher _events;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _clock;

    public CustomAuthorizationRuleService(
        ITenantUserService users,
        IUserManagementEventPublisher events,
        ITenantDbContext context,
        TimeProvider clock)
    {
        _users = users;
        _events = events;
        _context = context;
        _clock = clock;
    }

    // --- Data access (ICustomAuthorizationRuleService) ---

    public async Task<IReadOnlyList<CustomAuthorizationRule>> GetRulesForUserAsync(Guid userId, CancellationToken ct = default) =>
        await _context.CustomAuthorizationRules
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

    private async Task<CustomAuthorizationRule?> GetByIdAsync(Guid ruleId, CancellationToken ct = default) =>
        await _context.CustomAuthorizationRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RuleId == ruleId, ct);

    private async Task AddAsync(CustomAuthorizationRule rule, CancellationToken ct = default)
    {
        _context.CustomAuthorizationRules.Add(rule);
        await _context.SaveChangesAsync(ct);
    }

    private async Task UpdateAsync(CustomAuthorizationRule rule, CancellationToken ct = default)
    {
        _context.CustomAuthorizationRules.Update(rule);
        await _context.SaveChangesAsync(ct);
    }

    private async Task DeleteAsync(Guid ruleId, CancellationToken ct = default) =>
        await _context.CustomAuthorizationRules
            .Where(r => r.RuleId == ruleId)
            .ExecuteDeleteAsync(ct);

    // --- Use cases (create / update / delete with snapshot bump + audit) ---

    public async Task<CustomAuthorizationRule> CreateRuleAsync(
        Guid actorId,
        string actorPersona,
        Guid targetUserId,
        IReadOnlyList<string> allowedActions,
        IReadOnlyDictionary<string, IReadOnlyList<string>> parameterScopeAssignments,
        CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow();
        var user = await LoadAsync(targetUserId, ct);

        var rule = new CustomAuthorizationRule
        {
            RuleId = Guid.NewGuid(),
            UserId = targetUserId,
            AllowedActions = allowedActions,
            ParameterScopeAssignments = parameterScopeAssignments,
            CreatedBy = actorId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        BumpSnapshot(user, now);

        await _context.ExecuteAsync(async () =>
        {
            await AddAsync(rule, ct);
            await _users.UpdateAsync(user, ct);
            await _events.PublishAsync(
                RuleEvent("custom_rule.created", actorId, actorPersona, rule.UserId, now, oldValue: null, RuleSnapshot(rule)), ct);
        }, ct);

        return rule;
    }

    public async Task<CustomAuthorizationRule> UpdateRuleAsync(
        Guid actorId,
        string actorPersona,
        Guid targetUserId,
        Guid ruleId,
        IReadOnlyList<string> allowedActions,
        IReadOnlyDictionary<string, IReadOnlyList<string>> parameterScopeAssignments,
        CancellationToken ct = default)
    {
        var rule = await LoadRuleAsync(ruleId, targetUserId, ct);
        var now = _clock.GetUtcNow();
        var user = await LoadAsync(targetUserId, ct);

        // oldValue = the rule snapshot before it is overwritten below.
        var oldValue = RuleSnapshot(rule);

        rule.AllowedActions = allowedActions;
        rule.ParameterScopeAssignments = parameterScopeAssignments;
        rule.UpdatedAt = now;
        BumpSnapshot(user, now);

        await _context.ExecuteAsync(async () =>
        {
            await UpdateAsync(rule, ct);
            await _users.UpdateAsync(user, ct);
            await _events.PublishAsync(
                RuleEvent("custom_rule.updated", actorId, actorPersona, rule.UserId, now, oldValue, RuleSnapshot(rule)), ct);
        }, ct);

        return rule;
    }

    public async Task DeleteRuleAsync(
        Guid actorId,
        string actorPersona,
        Guid targetUserId,
        Guid ruleId,
        CancellationToken ct = default)
    {
        var rule = await LoadRuleAsync(ruleId, targetUserId, ct);
        var now = _clock.GetUtcNow();
        var user = await LoadAsync(targetUserId, ct);
        BumpSnapshot(user, now);

        await _context.ExecuteAsync(async () =>
        {
            await DeleteAsync(ruleId, ct);
            await _users.UpdateAsync(user, ct);
            // A delete records the removed rule as oldValue; there is no new state.
            await _events.PublishAsync(
                RuleEvent("custom_rule.deleted", actorId, actorPersona, rule.UserId, now, RuleSnapshot(rule), newValue: null), ct);
        }, ct);
    }

    private async Task<TenantUser> LoadAsync(Guid userId, CancellationToken ct) =>
        await _users.GetByIdAsync(userId, ct)
        ?? throw new KeyNotFoundException($"User {userId} does not exist.");

    private async Task<CustomAuthorizationRule> LoadRuleAsync(Guid ruleId, Guid targetUserId, CancellationToken ct)
    {
        var rule = await GetByIdAsync(ruleId, ct);
        // A rule that does not exist — or belongs to a different user — is "not found" for this route.
        if (rule is null || rule.UserId != targetUserId)
        {
            throw new KeyNotFoundException($"Custom rule {ruleId} does not exist for user {targetUserId}.");
        }

        return rule;
    }

    private static void BumpSnapshot(TenantUser user, DateTimeOffset now)
    {
        user.LastPermissionSnapshotVersion += 1;
        user.UpdatedAt = now;
    }

    private static object RuleSnapshot(CustomAuthorizationRule rule) =>
        new { rule.RuleId, rule.AllowedActions, rule.ParameterScopeAssignments };

    private static UserManagementEvent RuleEvent(
        string eventType, Guid actorId, string actorPersona, Guid entityUserId, DateTimeOffset now, object? oldValue, object? newValue) => new()
    {
        EventType = eventType,
        ActorId = actorId,
        ActorPersona = actorPersona,
        EntityType = nameof(CustomAuthorizationRule),
        EntityId = entityUserId,
        OldValue = oldValue,
        NewValue = newValue,
        OccurredAtUtc = now,
        CorrelationId = Guid.NewGuid(),
    };
}
