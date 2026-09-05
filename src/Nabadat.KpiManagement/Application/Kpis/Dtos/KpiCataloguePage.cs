using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Application.Kpis.Dtos;

/// <summary>
/// One page of the catalogue list: the ordered <see cref="Items"/> for this page plus the opaque
/// <see cref="NextCursor"/> (<c>null</c> when the catalogue is exhausted), per the cursor-pagination
/// contract (research.md R8).
/// </summary>
public sealed record KpiCataloguePage(IReadOnlyList<KpiDefinition> Items, string? NextCursor);
