using Nabadat.KpiManagement.Domain.Entities;
using Nabadat.KpiManagement.Domain.ValueObjects;
using Nabadat.KpiManagement.Application.Kpis.Dtos;

namespace Nabadat.KpiManagement.Application.Kpis.Services;

/// <summary>
/// Composes the catalogue list query (research.md R7): applies the <see cref="KpiTypeFilter"/>, the
/// active-only flag, and a trimmed case-insensitive substring search over <c>short_name ∪ full_name</c>,
/// then orders standards in canonical order (NPS, CSAT, CES, CXI, FCR, VFM, AgentScore, CHS) followed
/// by custom KPIs newest-first (<c>created_at DESC</c>, tie-broken by id).
/// <para>The canonical-rank expression is a ternary chain so EF Core translates it to a SQL
/// <c>CASE</c> while LINQ-to-Objects (the unit tests) evaluates it directly. The order here mirrors
/// <see cref="Catalogue.KpiSeedDataProvider.CanonicalShortNames"/> and the migration seed — both are
/// independently pinned by unit tests, so the two cannot silently drift. Cursor/limit pagination
/// (R8) is layered on top of this ordered query by the caller (see <see cref="KpiCatalogueCursor"/>).</para>
/// </summary>
public static class KpiCatalogueQuery
{
    public static IQueryable<KpiDefinition> Build(
        IQueryable<KpiDefinition> source,
        KpiTypeFilter type,
        bool activeOnly,
        string? search)
    {
        ArgumentNullException.ThrowIfNull(source);

        var query = type switch
        {
            KpiTypeFilter.Standard => source.Where(k => k.KpiType == KpiType.Standard),
            KpiTypeFilter.Custom => source.Where(k => k.KpiType == KpiType.Custom),
            _ => source,
        };

        if (activeOnly)
        {
            query = query.Where(k => k.IsActive);
        }

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var lowered = term.ToLower();
            query = query.Where(k =>
                k.ShortName.ToLower().Contains(lowered) ||
                k.FullName.ToLower().Contains(lowered));
        }

        return query
            .OrderBy(k =>
                k.ShortName == "NPS" ? 0 :
                k.ShortName == "CSAT" ? 1 :
                k.ShortName == "CES" ? 2 :
                k.ShortName == "CXI" ? 3 :
                k.ShortName == "FCR" ? 4 :
                k.ShortName == "VFM" ? 5 :
                k.ShortName == "AgentScore" ? 6 :
                k.ShortName == "CHS" ? 7 : 8)
            .ThenByDescending(k => k.CreatedAt)
            .ThenBy(k => k.Id);
    }
}
