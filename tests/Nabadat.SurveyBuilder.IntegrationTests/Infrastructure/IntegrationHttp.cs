using System.Net.Http.Json;
using System.Text.Json;

namespace Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;

/// <summary>
/// Small HTTP helpers shared by the M-01 API/scenario tests: sending writes with an
/// <c>If-Match</c> / <c>Idempotency-Key</c> header (which <c>HttpClient</c>'s typed
/// <c>PostAsJsonAsync</c> can't set per-request), reading a JSON body, and pulling the API-05
/// <c>error.code</c> out of a non-2xx envelope. Enum fields are integers on the wire (the platform
/// has no <c>JsonStringEnumConverter</c> — see CLAUDE.md Backend Integration).
/// </summary>
public static class IntegrationHttp
{
    public static async Task<HttpResponseMessage> PostJsonAsync(
        HttpClient client, string url, object? body = null, string? ifMatch = null, string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body ?? new { }),
        };
        AddHeaders(request, ifMatch, idempotencyKey);
        return await client.SendAsync(request);
    }

    public static async Task<HttpResponseMessage> PutJsonAsync(
        HttpClient client, string url, object body, string? ifMatch = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body) };
        AddHeaders(request, ifMatch, idempotencyKey: null);
        return await client.SendAsync(request);
    }

    public static async Task<HttpResponseMessage> PatchJsonAsync(
        HttpClient client, string url, object body, string? ifMatch = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = JsonContent.Create(body) };
        AddHeaders(request, ifMatch, idempotencyKey: null);
        return await client.SendAsync(request);
    }

    public static async Task<HttpResponseMessage> DeleteAsync(HttpClient client, string url, string? ifMatch = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        AddHeaders(request, ifMatch, idempotencyKey: null);
        return await client.SendAsync(request);
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>Reads <c>error.code</c> from an API-05 envelope body.</summary>
    public static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await ReadJsonAsync(response);
        return body.TryGetProperty("error", out var error) && error.TryGetProperty("code", out var code)
            ? code.GetString()
            : null;
    }

    private static void AddHeaders(HttpRequestMessage request, string? ifMatch, string? idempotencyKey)
    {
        if (!string.IsNullOrEmpty(ifMatch))
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }
    }
}
