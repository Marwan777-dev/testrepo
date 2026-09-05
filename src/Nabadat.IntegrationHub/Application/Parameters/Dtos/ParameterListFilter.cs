using Nabadat.IntegrationHub.Domain.ValueObjects;

namespace Nabadat.IntegrationHub.Application.Parameters.Dtos;

/// <summary>
/// SCR-05's three list filters, <b>AND-combined</b> (FR-S5-01, AC-S5-01): the origin tab, the type filter, and the
/// name/API-field search box. A <c>null</c> member means "not filtering on this".
/// </summary>
/// <param name="Origin">The active origin tab; <c>null</c> is the "All" tab.</param>
/// <param name="DataType">The type filter; <c>null</c> is "any type".</param>
/// <param name="Search">Matches <c>name_en</c>, <c>name_ar</c>, or <c>api_field</c>, case-insensitively (SCR-05's "Search by name or API field…").</param>
public sealed record ParameterListFilter(
    ParameterOrigin? Origin = null,
    DataType? DataType = null,
    string? Search = null)
{
    /// <summary>The unfiltered "All" tab.</summary>
    public static ParameterListFilter None { get; } = new();
}
