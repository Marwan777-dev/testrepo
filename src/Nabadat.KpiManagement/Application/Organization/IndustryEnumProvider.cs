using Nabadat.KpiManagement.Application.Organization.Interfaces;
using Nabadat.KpiManagement.Domain.ValueObjects;

namespace Nabadat.KpiManagement.Application.Organization;

/// <summary>
/// M-06's canonical industry list (FR-050 / R13) — the single source of truth. <see cref="GetAll"/>
/// returns the six <see cref="Industry"/> members in declaration (canonical) order;
/// <see cref="IsValid"/> accepts exactly those names. Stateless and allocation-light.
/// </summary>
public sealed class IndustryEnumProvider : IIndustryEnumProvider
{
    private static readonly IReadOnlyList<Industry> Canonical = Enum.GetValues<Industry>();
    private static readonly HashSet<string> CanonicalNames = new(Enum.GetNames<Industry>(), StringComparer.Ordinal);

    public IReadOnlyList<Industry> GetAll() => Canonical;

    // Match against the exact member names (case-sensitive) so numeric strings like "1" and
    // unknowns like "Aerospace" are both rejected.
    public bool IsValid(string? industry) =>
        !string.IsNullOrEmpty(industry) && CanonicalNames.Contains(industry);
}
