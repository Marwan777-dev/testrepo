using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Templates.Interfaces;

/// <summary>
/// Data-access port for the template aggregate + its 1:1 snapshot (DB-08 — the store is the only EF
/// seam; depends on <c>ITenantDbContext</c>). Implemented by <c>TemplateStore</c> (T190). Multi-write
/// atomicity (template row + snapshot) is the caller's concern via <c>ITenantDbContext.ExecuteAsync</c>.
/// </summary>
public interface ITemplateStore
{
    Task<Template?> GetAsync(Guid id, CancellationToken ct = default);

    Task<TemplateSnapshot?> GetSnapshotAsync(Guid templateId, CancellationToken ct = default);

    /// <summary>Adds the metadata row and its 1:1 snapshot together.</summary>
    Task AddAsync(Template template, TemplateSnapshot snapshot, CancellationToken ct = default);

    Task UpdateAsync(Template template, CancellationToken ct = default);

    Task UpdateSnapshotAsync(TemplateSnapshot snapshot, CancellationToken ct = default);

    /// <summary>Removes the template; the DB cascades the delete to <c>template_snapshots</c> (no cascade to instantiated surveys — BR-7.1).</summary>
    Task DeleteAsync(Template template, CancellationToken ct = default);

    /// <summary>Candidates for F6 search filtered by class/sector (ordering, name/tag filter, and paging are applied by <c>TemplateSearchService</c>).</summary>
    Task<IReadOnlyList<Template>> ListAsync(TemplateClass? cls, string? sector, CancellationToken ct = default);
}
