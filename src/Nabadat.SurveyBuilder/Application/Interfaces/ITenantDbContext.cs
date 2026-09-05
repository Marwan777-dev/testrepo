using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Application.Interfaces;

/// <summary>
/// Application-owned abstraction of the per-tenant EF context (implemented by
/// <c>TenantDbContext</c> in Infrastructure — T011). The M-01 per-entity services depend on this
/// interface — not the concrete context — so they live in the Application layer while the EF
/// context and entity mappings stay in Infrastructure (DB-08 / AMENDMENT-007, mirroring the M-06
/// / M-16 reference). Exposes the nine M-01 tenant-schema <see cref="DbSet{TEntity}"/>s,
/// <see cref="SaveChangesAsync"/>, and <see cref="ExecuteAsync(Func{Task}, CancellationToken)"/>
/// — the single multi-write transaction boundary (no unit-of-work type). Destructive
/// Return-to-Draft (BR-1.6), atomic reorders (FR-8.2), and question-delete translation scrubs
/// (FR-2.8) all run inside that boundary.
/// </summary>
public interface ITenantDbContext
{
    DbSet<Survey> Surveys { get; }

    DbSet<Section> Sections { get; }

    DbSet<QuestionsSet> QuestionsSets { get; }

    DbSet<Question> Questions { get; }

    DbSet<RoutingMap> RoutingMaps { get; }

    DbSet<Theme> Themes { get; }

    DbSet<SurveyTranslation> SurveyTranslations { get; }

    DbSet<Template> Templates { get; }

    DbSet<TemplateSnapshot> TemplateSnapshots { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="work"/> inside one transaction, then commits — rolling back if it
    /// throws. Per-entity services invoked inside persist themselves; because the transaction is
    /// open those saves only flush, and this single commit makes them all atomic. Single-write
    /// operations don't need this — the method's own save is already atomic.
    /// </summary>
    Task ExecuteAsync(Func<Task> work, CancellationToken ct = default);

    Task<T> ExecuteAsync<T>(Func<Task<T>> work, CancellationToken ct = default);
}
