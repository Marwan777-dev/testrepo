using Nabadat.IntegrationHub.Domain.ValueObjects;

namespace Nabadat.IntegrationHub.Application.Parameters.Dtos;

/// <summary>
/// The SCR-06 "New parameter" submission (<c>POST /api/v1/integration-hub/parameters</c>). Every created
/// parameter is <see cref="ParameterOrigin.Custom"/> — the 23 built-ins are seeded by the baseline (BR-23) and
/// there is no path that creates one.
/// </summary>
/// <param name="NameEn">Required, ≤ 50 chars (VR-F05). Drives the <paramref name="ApiField"/> auto-suggest.</param>
/// <param name="NameAr">Required, ≤ 50 chars (VR-F05), rendered RTL.</param>
/// <param name="ApiField">The <c>snake_case</c> wire key (VR-F06). Client-suggested, user-editable, then locked on first use (BR-11).</param>
/// <param name="DataType">One of the 13 ratified types (FR-F0-04).</param>
/// <param name="RangeMin">Range only — required there, forbidden elsewhere (VR-F07).</param>
/// <param name="RangeMax">Range only — required there, forbidden elsewhere (VR-F07).</param>
/// <param name="RangeUnit">Range only — optional label, e.g. "minutes".</param>
/// <param name="ValidationRule">Optional regex / per-type rule; a value failing it rejects the request with <c>E-1003</c>.</param>
/// <param name="Enabled">Defaults to on; a parameter may be created already disabled.</param>
/// <param name="RequiredByDefault">Usage flag 1 — the assignment default only; the channel contract is authoritative at request time (BR-08).</param>
/// <param name="Filterable">Usage flag 2 — defaults on (FR-S6-04).</param>
/// <param name="ReportingVisibility">Usage flag 3 — defaults on (FR-S6-04).</param>
/// <param name="DashboardVisibility">Usage flag 4 — defaults off (FR-S6-04).</param>
/// <param name="MappingSupport">
/// Usage flag 5 — a <b>request</b>, not the stored value: <see cref="MappingSupportPolicy"/> resolves it from the
/// data type (BR-27), so a contradicting client value is corrected server-side rather than rejected.
/// </param>
/// <param name="ChannelIds">SCR-06's channel-assignment pills — each adds the parameter as <b>supported</b> with the required-default applied (FR-S6-05).</param>
/// <param name="ActorId">The authenticated actor, for the M-17 audit row.</param>
/// <param name="ActorPersona">The actor's persona, for the M-17 audit row.</param>
public sealed record ParameterCreateCommand(
    string? NameEn,
    string? NameAr,
    string? ApiField,
    DataType DataType,
    decimal? RangeMin,
    decimal? RangeMax,
    string? RangeUnit,
    string? ValidationRule,
    bool Enabled,
    bool RequiredByDefault,
    bool Filterable,
    bool ReportingVisibility,
    bool DashboardVisibility,
    bool? MappingSupport,
    IReadOnlyList<Guid>? ChannelIds,
    Guid ActorId,
    string? ActorPersona);
