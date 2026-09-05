using System.Net.Http.Json;
using System.Text.Json;

namespace Nabadat.CustomerJourneyManagement.IntegrationTests.Infrastructure;

/// <summary>Small helpers for reading JSON bodies and the API-05 error envelope.</summary>
internal static class JsonHttp
{
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>Reads <c>error.code</c> from an API-05 envelope body.</summary>
    public static async Task<string?> ReadErrorCodeAsync(this HttpResponseMessage response)
    {
        var root = await response.Content.ReadFromJsonAsync<JsonElement>();
        return root.GetProperty("error").GetProperty("code").GetString();
    }
}
