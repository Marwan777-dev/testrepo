namespace Nabadat.IntegrationHub.Application.Parameters.Dtos;

/// <summary>
/// One API-04 cursor page of SCR-05's parameter list, plus the <b>global</b> origin-tab counts that ride along on
/// every page (FR-S5-01) so the tabs render without a second round-trip.
/// </summary>
/// <param name="NextCursor"><c>null</c> when this is the last page.</param>
public sealed record ParameterPage(
    IReadOnlyList<ParameterDto> Items,
    string? NextCursor,
    ParameterOriginCounts Counts);
