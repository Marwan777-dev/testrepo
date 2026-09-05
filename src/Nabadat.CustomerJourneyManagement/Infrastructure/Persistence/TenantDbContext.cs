using Microsoft.EntityFrameworkCore;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Infrastructure.Persistence.Configurations;

namespace Nabadat.CustomerJourneyManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core context over the per-tenant PostgreSQL schema (<c>ConnectionStrings:TenantDb</c>).
///
/// <para>The context <b>is</b> the unit of work (database-constitution Article 7 / router
/// DB-08): the data-access services inject it through <see cref="ITenantDbContext"/> and call
/// <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> — there is no repository layer
/// and no separate <c>ITransactionRunner</c>/<c>IUnitOfWork</c> abstraction anymore. A
/// change-tracked graph persisted by one <c>SaveChangesAsync</c> is one transaction; that is
/// the atomicity boundary (the M-17 audit <see cref="EventLog"/> is tracked in the same
/// context as its business change, so it commits with it — FR-015).</para>
///
/// <para>It maps onto the existing raw-SQL baseline schema (<c>001_customer_journey_baseline.sql</c>) and
/// owns no EF migrations. Entity→table mapping lives in one
/// <c>IEntityTypeConfiguration&lt;T&gt;</c> per entity under <c>Configurations/</c>.</para>
///
/// <para>The per-request tenant schema is selected by the shared
/// <c>TenantSchemaConnectionInterceptor</c> (reused from the M-10 module) which issues
/// <c>SET search_path</c> per connection open (AD-02 / DB-01); in single-tenant mode it
/// no-ops onto the host's default schema.</para>
/// </summary>
public sealed class TenantDbContext : DbContext, ITenantDbContext
{
    public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options)
    {
    }

    public DbSet<Journey> Journeys => Set<Journey>();

    public DbSet<Stage> Stages => Set<Stage>();

    public DbSet<Touchpoint> Touchpoints => Set<Touchpoint>();

    public DbSet<KpiBinding> KpiBindings => Set<KpiBinding>();

    public DbSet<KpiTypeDefinition> KpiTypeDefinitions => Set<KpiTypeDefinition>();

    public DbSet<ScoringConfig> ScoringConfigs => Set<ScoringConfig>();

    public DbSet<Persona> Personas => Set<Persona>();

    public DbSet<JourneyPersonaBinding> JourneyPersonaBindings => Set<JourneyPersonaBinding>();

    public DbSet<JourneyVersion> JourneyVersions => Set<JourneyVersion>();

    public DbSet<DetectionConfig> DetectionConfigs => Set<DetectionConfig>();

    public DbSet<DetectionThresholdOverride> DetectionThresholdOverrides => Set<DetectionThresholdOverride>();

    public DbSet<ReportContract> ReportContracts => Set<ReportContract>();

    public DbSet<JourneyScore> JourneyScores => Set<JourneyScore>();

    /// <summary>M-17's <c>event_log</c>; M-16 appends audit rows here within the same unit of work.</summary>
    public DbSet<EventLog> EventLogs => Set<EventLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Explicit per-context registration (the control-plane context lives in the same
        // assembly, so ApplyConfigurationsFromAssembly would wrongly bleed configs across).
        modelBuilder.ApplyConfiguration(new JourneyConfiguration());
        modelBuilder.ApplyConfiguration(new StageConfiguration());
        modelBuilder.ApplyConfiguration(new TouchpointConfiguration());
        modelBuilder.ApplyConfiguration(new KpiBindingConfiguration());
        modelBuilder.ApplyConfiguration(new KpiTypeDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new ScoringConfigConfiguration());
        modelBuilder.ApplyConfiguration(new PersonaConfiguration());
        modelBuilder.ApplyConfiguration(new JourneyPersonaBindingConfiguration());
        modelBuilder.ApplyConfiguration(new JourneyVersionConfiguration());
        modelBuilder.ApplyConfiguration(new DetectionConfigConfiguration());
        modelBuilder.ApplyConfiguration(new DetectionThresholdOverrideConfiguration());
        modelBuilder.ApplyConfiguration(new ReportContractConfiguration());
        modelBuilder.ApplyConfiguration(new JourneyScoreConfiguration());
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
