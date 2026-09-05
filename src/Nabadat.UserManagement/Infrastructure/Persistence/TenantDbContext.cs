using Microsoft.EntityFrameworkCore;
using Nabadat.UserManagement.Application.Interfaces;
using Nabadat.UserManagement.Domain.Entities;
using Nabadat.UserManagement.Infrastructure.Persistence.Configurations;

namespace Nabadat.UserManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core context over the per-tenant PostgreSQL schema (<c>ConnectionStrings:TenantDb</c>).
///
/// <para>The context <b>is</b> the unit of work (database-constitution Article 7 / router
/// DB-08): services inject it directly and call <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> —
/// there is no repository layer and no separate <c>IUnitOfWork</c> abstraction. A
/// change-tracked graph persisted by one <c>SaveChangesAsync</c> is one transaction;
/// that is the atomicity boundary (the M-17 audit <see cref="EventLog"/> is tracked in
/// the same context as its business change).</para>
///
/// <para>It maps onto the existing raw-SQL baseline schema and owns no EF migrations.
/// Entity→table mapping lives in one <c>IEntityTypeConfiguration&lt;T&gt;</c> per entity
/// under <c>Configurations/</c>, discovered by <see cref="ModelBuilder.ApplyConfigurationsFromAssembly"/>.</para>
///
/// <para>Control-plane tables live in a separate <c>ControlPlaneDbContext</c>; a
/// control-plane write is its own <c>SaveChangesAsync</c> and is never atomic with a
/// tenant write.</para>
/// </summary>
public sealed class TenantDbContext : DbContext, ITenantDbContext
{
    public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options)
    {
    }

    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();

    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<PasswordResetRateLimitRecord> PasswordResetRateLimits => Set<PasswordResetRateLimitRecord>();

    public DbSet<PermissionModuleAssignment> PermissionModuleAssignments => Set<PermissionModuleAssignment>();

    public DbSet<CustomAuthorizationRule> CustomAuthorizationRules => Set<CustomAuthorizationRule>();

    public DbSet<DataScopeAssignment> DataScopeAssignments => Set<DataScopeAssignment>();

    public DbSet<DataScopeParameterDefinition> DataScopeParameterDefinitions => Set<DataScopeParameterDefinition>();

    public DbSet<OrganizationHierarchyNode> OrganizationHierarchyNodes => Set<OrganizationHierarchyNode>();

    /// <summary>M-17's <c>event_log</c>; M-10 appends audit rows here within the same unit of work.</summary>
    public DbSet<EventLog> EventLogs => Set<EventLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Explicit per-context registration: both contexts live in one assembly, so
        // ApplyConfigurationsFromAssembly would wrongly bleed control-plane configs in here.
        modelBuilder.ApplyConfiguration(new TenantUserConfiguration());
        modelBuilder.ApplyConfiguration(new AuthSessionConfiguration());
        modelBuilder.ApplyConfiguration(new PasswordResetTokenConfiguration());
        modelBuilder.ApplyConfiguration(new PasswordResetRateLimitRecordConfiguration());
        modelBuilder.ApplyConfiguration(new PermissionModuleAssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new CustomAuthorizationRuleConfiguration());
        modelBuilder.ApplyConfiguration(new DataScopeAssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new DataScopeParameterDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationHierarchyNodeConfiguration());
        modelBuilder.ApplyConfiguration(new EventLogConfiguration());
    }

    public async Task ExecuteAsync(Func<Task> work, CancellationToken ct = default)
    {
        await using var transaction = await Database.BeginTransactionAsync(ct);
        try
        {
            await work();
            await SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> work, CancellationToken ct = default)
    {
        await using var transaction = await Database.BeginTransactionAsync(ct);
        try
        {
            var result = await work();
            await SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
