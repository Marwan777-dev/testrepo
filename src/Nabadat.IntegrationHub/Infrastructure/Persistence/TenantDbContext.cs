using Microsoft.EntityFrameworkCore;
using Nabadat.IntegrationHub.Application.Interfaces;
using Nabadat.IntegrationHub.Domain.Entities;
using Nabadat.IntegrationHub.Infrastructure.Persistence.Configurations;

namespace Nabadat.IntegrationHub.Infrastructure.Persistence;

/// <summary>
/// EF Core context over the per-tenant PostgreSQL schema (<c>ConnectionStrings:TenantDb</c>) for the
/// eight M-13 tables plus the shared M-17 <c>event_log</c>.
///
/// <para>The context <b>is</b> the unit of work (DB-08 / AMENDMENT-007): the per-aggregate services
/// inject it through <see cref="ITenantDbContext"/> and call
/// <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> — there is no repository layer and no
/// separate unit-of-work type. A change-tracked graph persisted by one <c>SaveChangesAsync</c> is one
/// transaction; <see cref="ExecuteAsync(Func{Task}, CancellationToken)"/> widens that boundary across
/// several writes (integration + first credential; revoke-old + generate-new, BR-16; a channel and its
/// contract rows; the all-or-nothing mapping import; any write plus its M-17 audit row).</para>
///
/// <para>It maps onto the existing raw-SQL baseline schema (<c>IntegrationHub_Baseline.sql</c>) and owns
/// no EF migrations. Entity→table mapping lives in one <c>IEntityTypeConfiguration&lt;T&gt;</c> per
/// entity under <c>Configurations/</c>.</para>
///
/// <para>The per-request tenant schema is selected by the shared
/// <c>TenantSchemaConnectionInterceptor</c> (reused from the M-10 module), which issues
/// <c>SET search_path</c> per connection open (AD-02 / DB-01).</para>
/// </summary>
public sealed class TenantDbContext : DbContext, ITenantDbContext
{
    public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options)
    {
    }

    public DbSet<ServiceChannel> ServiceChannels => Set<ServiceChannel>();

    public DbSet<Parameter> Parameters => Set<Parameter>();

    public DbSet<ChannelParameterAssignment> ChannelParameterAssignments => Set<ChannelParameterAssignment>();

    public DbSet<ParameterMapping> ParameterMappings => Set<ParameterMapping>();

    public DbSet<UnmappedValueOccurrence> UnmappedValueOccurrences => Set<UnmappedValueOccurrence>();

    public DbSet<Integration> Integrations => Set<Integration>();

    public DbSet<Credential> Credentials => Set<Credential>();

    /// <summary>Append-only and DB-04 monthly-partitioned — inserts and reads only.</summary>
    public DbSet<IntegrationRequestLog> IntegrationRequestLogs => Set<IntegrationRequestLog>();

    /// <summary>M-17's shared <c>event_log</c>; M-13 appends configuration-change rows in the same unit of work as the change.</summary>
    public DbSet<EventLog> EventLogs => Set<EventLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Explicit per-context registration (avoids ApplyConfigurationsFromAssembly bleeding configs
        // across contexts if a second context is ever added to this assembly).
        modelBuilder.ApplyConfiguration(new ServiceChannelConfiguration());
        modelBuilder.ApplyConfiguration(new ParameterConfiguration());
        modelBuilder.ApplyConfiguration(new ChannelParameterAssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new ParameterMappingConfiguration());
        modelBuilder.ApplyConfiguration(new UnmappedValueOccurrenceConfiguration());
        modelBuilder.ApplyConfiguration(new IntegrationConfiguration());
        modelBuilder.ApplyConfiguration(new CredentialConfiguration());
        modelBuilder.ApplyConfiguration(new IntegrationRequestLogConfiguration());
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
