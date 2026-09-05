using System.Text.Json.Serialization;

namespace Nabadat.IntegrationHub.Api.Contracts;

/// <summary>
/// <c>PATCH /api/v1/integration-hub/parameters/{id}</c>'s 200 body. It carries the parameter plus BR-10's impact
/// information, because the impact warning has <b>two</b> distinct 200-shaped outcomes and the client must be able
/// to tell them apart:
///
/// <list type="number">
///   <item><c>requires_confirmation: true</c> — the disable was <b>withheld</b>; render Dialog D-6 with
///   <c>references</c> and re-send with <c>confirm_disable</c> once the user accepts. <c>parameter</c> is the
///   <i>unchanged</i> row.</item>
///   <item><c>requires_confirmation: false</c> with a non-empty <c>references</c> — the disable was
///   <b>applied</b> (the user had already confirmed); the list is informational.</item>
/// </list>
///
/// <para>contracts/api-endpoints.md left this wire shape open ("response-includes-list vs. a required
/// <c>confirm=true</c> re-call"); this is the resolved choice, and it is deliberately <b>not</b> a 4xx — BR-10
/// calls for a warning, not a rejection, so a status that reads as failure would misrepresent it.</para>
/// </summary>
public sealed record PatchParameterResponse
{
    [JsonPropertyName("parameter")]
    public ParameterResponse Parameter { get; init; } = new();

    /// <summary>True when the change was withheld pending the user's acknowledgement of Dialog D-6.</summary>
    [JsonPropertyName("requires_confirmation")]
    public bool RequiresConfirmation { get; init; }

    /// <summary>Empty unless a disable touched existing consumers.</summary>
    [JsonPropertyName("references")]
    public IReadOnlyList<ParameterReferenceResponse> References { get; init; } =
        Array.Empty<ParameterReferenceResponse>();
}
