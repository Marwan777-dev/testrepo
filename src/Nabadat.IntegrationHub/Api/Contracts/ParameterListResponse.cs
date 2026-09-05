using System.Text.Json.Serialization;

namespace Nabadat.IntegrationHub.Api.Contracts;

/// <summary>
/// <c>GET /api/v1/integration-hub/parameters</c> — one API-04 cursor page plus SCR-05's origin-tab counts.
/// </summary>
public sealed record ParameterListResponse
{
    [JsonPropertyName("items")]
    public IReadOnlyList<ParameterResponse> Items { get; init; } = Array.Empty<ParameterResponse>();

    /// <summary><c>null</c> on the last page.</summary>
    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; init; }

    /// <summary>
    /// FR-S5-01's tab counts — <b>global</b>, deliberately unaffected by the origin/type/search filters
    /// (AC-S5-01), so the tabs stay a navigation affordance rather than a second result count.
    /// </summary>
    [JsonPropertyName("counts")]
    public ParameterCountsResponse Counts { get; init; } = new();
}
