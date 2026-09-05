using Nabadat.IntegrationHub.Domain.ValueObjects;

namespace Nabadat.IntegrationHub.Application.Parameters.Dtos;

/// <summary>
/// The <c>PATCH /api/v1/integration-hub/parameters/{id}</c> submission: SCR-05's inline enable/disable toggle and
/// SCR-06's edit drawer share one endpoint.
///
/// <para><b>Every field is nullable and <c>null</c> means "not submitted"</b> — that is what makes this a genuine
/// PATCH rather than a PUT wearing its name. It matters for three rules: a locked parameter's read-only form does
/// not send <see cref="ApiField"/> at all (so <c>ApiFieldNameLockGuard</c> must not read the omission as a
/// change); a built-in's read-only type select does not send <see cref="DataType"/> (so
/// <c>BuiltInParameterGuard</c> is only consulted when the client actually asks for a change); and the inline
/// toggle sends <see cref="Enabled"/> and nothing else.</para>
/// </summary>
/// <param name="ConfirmDisable">
/// BR-10 — set once the user has acknowledged Dialog D-6. Absent or <c>false</c> on a disable that has references,
/// the endpoint returns <b>200</b> with the reference list and <b>does not apply the change</b>; the client
/// re-sends with this flag set. (contracts/api-endpoints.md leaves the wire shape to implementation time; this is
/// the resolved choice — see <c>ParameterService.PatchAsync</c>.)
/// </param>
public sealed record ParameterPatchCommand(
    string? NameEn,
    string? NameAr,
    string? ApiField,
    DataType? DataType,
    decimal? RangeMin,
    decimal? RangeMax,
    string? RangeUnit,
    string? ValidationRule,
    bool? Enabled,
    bool? RequiredByDefault,
    bool? Filterable,
    bool? ReportingVisibility,
    bool? DashboardVisibility,
    bool? MappingSupport,
    IReadOnlyList<Guid>? ChannelIds,
    bool ConfirmDisable,
    Guid ActorId,
    string? ActorPersona);
