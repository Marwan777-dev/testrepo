using Nabadat.IntegrationHub.Domain.ValueObjects;

namespace Nabadat.IntegrationHub.Application.Parameters.Dtos;

/// <summary>
/// One parameter as SCR-05's table row and SCR-06's drawer read it (FR-S5-02, FR-S6-02…05).
///
/// <para>The two lock flags and <see cref="MappingSupportChangeable"/> are <b>derived server-side</b> and carried
/// on the wire on purpose: the console must render the API-field input read-only, the type select read-only, and
/// the Mapping-support switch disabled without re-deriving BR-09/BR-11/BR-27 in TypeScript. The server stays the
/// single authority; the client only reflects it.</para>
/// </summary>
/// <param name="ApiFieldLocked">BR-11 — the wire key can no longer change (built-ins: always true, BR-09).</param>
/// <param name="DataTypeLocked">
/// <c>[PO-G27]</c> — derived from <c>origin = built_in</c>, never stored, so the two cannot drift.
/// </param>
/// <param name="MappingSupportChangeable">BR-27 — whether SCR-06 may offer the Mapping-support switch at all.</param>
/// <param name="MappingsCount">Rows in the parameter's mapping table; drives SCR-05's "Mapped" link vs "—".</param>
/// <param name="ChannelIds">The service channels whose contract includes this parameter (FR-S5-02's Channels count).</param>
public sealed record ParameterDto(
    Guid Id,
    string NameEn,
    string NameAr,
    string ApiField,
    bool ApiFieldLocked,
    DataType DataType,
    bool DataTypeLocked,
    decimal? RangeMin,
    decimal? RangeMax,
    string? RangeUnit,
    string? ValidationRule,
    ParameterOrigin Origin,
    bool Enabled,
    bool RequiredByDefault,
    bool Filterable,
    bool ReportingVisibility,
    bool DashboardVisibility,
    bool MappingSupport,
    bool MappingSupportChangeable,
    int MappingsCount,
    IReadOnlyList<Guid> ChannelIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
