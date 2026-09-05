using Microsoft.EntityFrameworkCore;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Infrastructure.Persistence;

/// <summary>
/// EF Core context over the per-tenant PostgreSQL schema (<c>ConnectionStrings:TenantDb</c>) for
/// the nine M-01 tables.
///
/// <para>The context <b>is</b> the unit of work (DB-08 / AMENDMENT-007): the per-entity services
/// inject it through <see cref="ITenantDbContext"/> and call
/// <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> — there is no repository layer and
/// no separate unit-of-work type. A change-tracked graph persisted by one <c>SaveChangesAsync</c>
/// is one transaction; <see cref="ExecuteAsync(Func{Task}, CancellationToken)"/> widens that
/// boundary across several writes (a destructive Return-to-Draft, an atomic reorder, a
/// question-delete + translation scrub).</para>
///
/// <para>It maps onto the existing raw-SQL baseline schema
/// (<c>Infrastructure/Migrations/_Baseline.sql</c>, T008) and owns no EF migrations (DB-08 rule 6).
/// Entity→table mapping lives in one <c>IEntityTypeConfiguration&lt;T&gt;</c> per entity under
/// <c>Configurations/</c>, applied in <see cref="OnModelCreating"/> — those configuration types
/// land per-entity in the US1+ phases (T058+).</para>
///
/// <para>The per-request tenant schema is selected by the shared
/// <c>TenantSchemaConnectionInterceptor</c> (reused from the M-10 module) which issues
/// <c>SET search_path</c> per connection open (AD-02 / DB-01); it is added to the
/// <c>DbContextOptions</c> at registration in the composition root.</para>
/// </summary>
public sealed class TenantDbContext : DbContext, ITenantDbContext
{
    public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options)
    {
    }

    public DbSet<Survey> Surveys => Set<Survey>();

    public DbSet<Section> Sections => Set<Section>();

    public DbSet<QuestionsSet> QuestionsSets => Set<QuestionsSet>();

    public DbSet<Question> Questions => Set<Question>();

    public DbSet<RoutingMap> RoutingMaps => Set<RoutingMap>();

    public DbSet<Theme> Themes => Set<Theme>();

    public DbSet<SurveyTranslation> SurveyTranslations => Set<SurveyTranslation>();

    public DbSet<Template> Templates => Set<Template>();

    public DbSet<TemplateSnapshot> TemplateSnapshots => Set<TemplateSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Explicit per-context registration (avoids ApplyConfigurationsFromAssembly bleeding
        // configs across contexts if a second context is ever added to this assembly).
        modelBuilder.ApplyConfiguration(new Configurations.SurveyConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.SectionConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.QuestionConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.ThemeConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.RoutingMapConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.QuestionsSetConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.TranslationConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.TemplateConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.TemplateSnapshotConfiguration());

        base.OnModelCreating(modelBuilder);
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
