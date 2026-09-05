using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Nabadat.IntegrationHub.IntegrationTests.Infrastructure;

/// <summary>Small helpers for reading JSON bodies and the API-05 error envelope (mirrors the M-06 lane).</summary>
internal static class JsonHttp
{
    /// <summary>
    /// Asserts the status code and, on mismatch, includes the response body in the failure message. Worth the
    /// extra await over a bare <c>StatusCode.Should().Be(...)</c>: an unexpected 4xx carries the API-05 code
    /// that explains it, and an unexpected 500 carries the server's envelope — without this the failure reads
    /// only "expected 201, found 500" and every diagnosis needs a second run.
    /// </summary>
    public static async Task ShouldHaveStatusAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode == expected)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(expected, "response body was: {0}", body);
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>Reads <c>error.code</c> from an API-05 envelope body.</summary>
    public static async Task<string?> ReadErrorCodeAsync(this HttpResponseMessage response)
    {
        var root = await response.Content.ReadFromJsonAsync<JsonElement>();
        return root.GetProperty("error").GetProperty("code").GetString();
    }

    /// <summary>Reads the <c>error.details[].code</c> list from an API-05 envelope body (empty when absent).</summary>
    public static async Task<IReadOnlyList<string>> ReadErrorDetailCodesAsync(this HttpResponseMessage response)
    {
        var root = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (!root.GetProperty("error").TryGetProperty("details", out var details)
            || details.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return details
            .EnumerateArray()
            .Select(detail => detail.GetProperty("code").GetString() ?? string.Empty)
            .ToList();
    }
}
