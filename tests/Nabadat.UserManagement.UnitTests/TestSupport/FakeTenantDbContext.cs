using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Domain.Entities;

namespace Nabadat.UserManagement.UnitTests.TestSupport;

/// <summary>
/// Test double for <see cref="ITenantDbContext"/>: <see cref="ExecuteAsync"/> runs the work
/// delegate directly (no real transaction). The <c>DbSet</c>s are not used by business-service
/// unit tests — those mock the data-access ports — so they are left unset. Real
/// commit/rollback is verified in the integration lane.
/// </summary>
internal sealed class FakeTenantDbContext : ITenantDbContext
{
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

    public Task ExecuteAsync(Func<Task> work, CancellationToken ct = default) => work();

    public Task<T> ExecuteAsync<T>(Func<Task<T>> work, CancellationToken ct = default) => work();
}
