using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Application.Interfaces;

namespace Nabadat.UserManagement.UnitTests.TestSupport;

/// <summary>
/// Test double for <see cref="ITenantDbContext"/> that runs the work delegate directly but
/// records whether the unit of work reached commit. <see cref="Committed"/> flips to
/// <c>true</c> only after the delegate completes without throwing — so a collaborator that
/// throws mid-delegate leaves it <c>false</c>, modelling the real transaction's rollback.
/// <see cref="ExecuteCount"/> proves the action wraps its writes in a <i>single</i>
/// transaction (FR-015). Real DB commit/rollback is verified in the integration lane.
/// </summary>
internal sealed class RecordingTenantDbContext : ITenantDbContext
{
    public int ExecuteCount { get; private set; }

    public bool Committed { get; private set; }

    public DbSet<TenantUser> TenantUsers => null!;
    public DbSet<AuthSession> AuthSessions => null!;
    public DbSet<PasswordResetToken> PasswordResetTokens => null!;
    public DbSet<PasswordResetRateLimitRecord> PasswordResetRateLimits => null!;
    public DbSet<PermissionModuleAssignment> PermissionModuleAssignments => null!;
    public DbSet<CustomAuthorizationRule> CustomAuthorizationRules => null!;
    public DbSet<DataScopeAssignment> DataScopeAssignments => null!;
    public DbSet<DataScopeParameterDefinition> DataScopeParameterDefinitions => null!;
    public DbSet<OrganizationHierarchyNode> OrganizationHierarchyNodes => null!;
    public DbSet<EventLog> EventLogs => null!;
    public DatabaseFacade Database => null!;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public async Task ExecuteAsync(Func<Task> work, CancellationToken ct = default)
    {
        ExecuteCount++;
        await work();
        Committed = true;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> work, CancellationToken ct = default)
    {
        ExecuteCount++;
        var result = await work();
        Committed = true;
        return result;
    }
}
