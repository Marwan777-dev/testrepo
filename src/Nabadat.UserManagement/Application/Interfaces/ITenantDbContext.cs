using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Nabadat.UserManagement.Domain.Entities;

namespace Nabadat.UserManagement.Application.Interfaces;

/// <summary>
/// Application-owned abstraction of the per-tenant EF context (implemented by
/// <c>TenantDbContext</c> in Infrastructure). The data-access services depend on this
/// interface — not the concrete context — so they live in the Application layer while the
/// EF context and entity mappings stay in Infrastructure. Exposes the tenant-schema
/// <see cref="DbSet{T}"/>s, <see cref="SaveChangesAsync"/>, and <see cref="ExecuteAsync"/>
/// — the transaction boundary used to make a multi-step operation (a business write and
/// its M-17 audit row) atomic.
/// </summary>
public interface ITenantDbContext
{
    DbSet<TenantUser> TenantUsers { get; }

    DbSet<AuthSession> AuthSessions { get; }

    DbSet<PasswordResetToken> PasswordResetTokens { get; }

    DbSet<PasswordResetRateLimitRecord> PasswordResetRateLimits { get; }

    DbSet<PermissionModuleAssignment> PermissionModuleAssignments { get; }

    DbSet<CustomAuthorizationRule> CustomAuthorizationRules { get; }

    DbSet<DataScopeAssignment> DataScopeAssignments { get; }

    DbSet<DataScopeParameterDefinition> DataScopeParameterDefinitions { get; }

    DbSet<OrganizationHierarchyNode> OrganizationHierarchyNodes { get; }

    DbSet<EventLog> EventLogs { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="work"/> inside one transaction, then commits — rolling back if it
    /// throws. The data-access methods invoked inside persist themselves; because the
    /// transaction is open those saves only flush, and this single commit makes them all
    /// atomic (a business write and its M-17 audit row commit or roll back together, FR-015).
    /// Single-write operations don't need this — the method's own save is already atomic.
    /// </summary>
    Task ExecuteAsync(Func<Task> work, CancellationToken ct = default);

    Task<T> ExecuteAsync<T>(Func<Task<T>> work, CancellationToken ct = default);
}
