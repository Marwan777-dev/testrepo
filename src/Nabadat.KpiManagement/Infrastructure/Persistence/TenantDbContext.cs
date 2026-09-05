using Microsoft.EntityFrameworkCore;
using Nabadat.KpiManagement.Application.Interfaces;
using Nabadat.KpiManagement.Domain.Entities;
using Nabadat.KpiManagement.Infrastructure.Persistence.Configurations;

namespace Nabadat.KpiManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core context over the per-tenant PostgreSQL schema (<c>ConnectionStrings:TenantDb</c>) for
/// the four M-06 tables.
///
/// <para>The context <b>is</b> the unit of work (DB-08 / AMENDMENT-007): the per-entity services
/// inject it through <see cref="ITenantDbContext"/> and call
/// <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> — there is no repository layer and
/// no separate unit-of-work type. A change-tracked graph persisted by one
/// <c>SaveChangesAsync</c> is one transaction; <see cref="ExecuteAsync(Func{Task}, CancellationToken)"/>
/// widens that boundary across several writes (a KPI save plus its M-17 audit row, data-model.md §8).</para>
///
/// <para>It maps onto the existing raw-SQL baseline schema (<c>KpiManagement_Baseline.sql</c>) and
/// owns no EF migrations. Entity→table mapping lives in one
/// <c>IEntityTypeConfiguration&lt;T&gt;</c> per entity under <c>Configurations/</c>.</para>
///
/// <para>The per-request tenant schema is selected by the shared
/// <c>TenantSchemaConnectionInterceptor</c> (reused from the M-10 module) which issues
/// <c>SET search_path</c> per connection open (AD-02 / DB-01).</para>
/// </summary>
public sealed class TenantDbContext : DbContext, ITenantDbContext
{
    public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options)
    {
    }

    public DbSet<KpiDefinition> KpiDefinitions => Set<KpiDefinition>();

    public DbSet<KpiThreshold> KpiThresholds => Set<KpiThreshold>();

    public DbSet<KpiPerspective> KpiPerspectives => Set<KpiPerspective>();

    public DbSet<CxiWeight> CxiWeights => Set<CxiWeight>();

    /// <summary>The tenant's singleton Organization settings (US-6, data-model.md §2.1).</summary>
    public DbSet<OrganizationSettings> OrganizationSettings => Set<OrganizationSettings>();

    /// <summary>M-17's shared <c>event_log</c>; M-06 appends <c>settings.changed</c> audit rows in
    /// the same unit of work as the KPI/settings write (data-model.md §8).</summary>
    public DbSet<EventLog> EventLogs => Set<EventLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Explicit per-context registration (avoids ApplyConfigurationsFromAssembly bleeding
        // configs across contexts if a second context is ever added to this assembly).
        modelBuilder.ApplyConfiguration(new KpiDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new KpiThresholdConfiguration());
        modelBuilder.ApplyConfiguration(new KpiPerspectiveConfiguration());
        modelBuilder.ApplyConfiguration(new CxiWeightConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationSettingsConfiguration());
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
