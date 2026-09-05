namespace Nabadat.KpiManagement.Domain.Entities;

/// <summary>
/// A named perspective (sub-dimension) of a KPI (tenant-schema table <c>kpi_perspectives</c>,
/// data-model.md §1.3). 0..10 rows per KPI, ordered by <see cref="DisplayOrder"/>. This feature
/// persists definitions only — per-perspective score storage is deferred to a later M-06 release.
/// Save semantics are full-replace (FR-028): the application deletes all existing rows and inserts
/// the new set in one transaction (FK ON DELETE CASCADE from <c>kpi_definitions</c>).
/// </summary>
public sealed class KpiPerspective
{
    public Guid Id { get; set; }

    /// <summary>Owning KPI (FK → <c>kpi_definitions.id</c>, ON DELETE CASCADE).</summary>
    public Guid KpiId { get; set; }

    /// <summary>Perspective label, ≤ 60 chars; free-text, tenant-language at authoring time.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Zero-based display order within the KPI's perspective list.</summary>
    public short DisplayOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
