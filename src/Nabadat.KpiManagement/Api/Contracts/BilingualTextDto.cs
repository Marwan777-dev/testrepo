using System.Text.Json.Serialization;

namespace Nabadat.KpiManagement.Api.Contracts;

/// <summary>Wire shape of a bilingual (EN + AR) anchor label on KPI create/read/update.</summary>
public sealed record BilingualTextDto
{
    [JsonPropertyName("en")]
    public string? En { get; init; }

    [JsonPropertyName("ar")]
    public string? Ar { get; init; }
}
