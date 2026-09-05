using System.Net.Http.Json;
using System.Text.Json;
using Nabadat.IntegrationHub.Application.Parameters.Dtos;
using Nabadat.IntegrationHub.Application.Parameters.Interfaces;

namespace Nabadat.IntegrationHub.Infrastructure.UserManagementIntegration;

/// <summary>
/// T059 — the transport half of M-13's <b>real</b> call to M-10: <c>POST</c>s a batch of scope parameter
/// definitions to <c>Nabadat.UserManagement</c>'s already-built
/// <c>POST /api/v1/authorization/scope/parameters</c> (research.md §4.1, CMC-06).
///
/// <para><b>Why HTTP and not an in-process call</b>, given M-10 is referenced by this project: the endpoint is
/// M-10's <i>published</i> integration surface — explicitly documented as "pushed by an external scope provider
/// (M-13)" — and it is <c>[AllowAnonymous]</c> because it is a service-to-service call authenticated at the
/// gateway. Calling <c>M13ParameterContractAdapter</c> directly would bypass M-10's own controller validation and
/// bind M-13 to an <i>Application-layer</i> type of another module, which the dependency rules forbid. It also
/// keeps the integration honest the day the two modules are deployed as separate processes (AD-05).</para>
///
/// <para>The camelCase naming policy is explicit rather than inherited: this client serialises for <b>another
/// service's</b> contract, so it must not silently change if the host's own <c>JsonSerializerOptions</c> are
/// reconfigured. M-10's <c>M13ParameterPayload</c> binds <c>sourceModule</c> / <c>parameters[].name</c> /
/// <c>label</c> / <c>allowedValues</c>.</para>
/// </summary>
public sealed class DataScopeHttpClient : IDataScopeContractClient
{
    /// <summary>The named <see cref="HttpClient"/> registered in the composition root.</summary>
    public const string ClientName = "M13.DataScopeContract";

    /// <summary>M-10's published route (API-01 <c>/api/v1/</c> prefix).</summary>
    public const string Route = "api/v1/authorization/scope/parameters";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _http;

    public DataScopeHttpClient(HttpClient http)
    {
        _http = http;
    }

    /// <inheritdoc />
    public async Task PublishAsync(DataScopeContractPayload payload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (_http.BaseAddress is null)
        {
            // Deliberately an exception, not a silent no-op: DataScopeContractPublisher catches and logs it, so a
            // misconfigured environment shows up in the log rather than as a quietly missing M-10 projection.
            throw new InvalidOperationException(
                "The M-10 data-scope base address is not configured (UserManagement:BaseUrl).");
        }

        using var response = await _http.PostAsJsonAsync(Route, payload, Json, ct);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // M-10 fails a payload WHOLESALE on one bad definition (reserved name, empty value set, >500 rows), so the
        // body is the only way to tell which rule tripped. Carrying it into the exception message is what makes a
        // rejected batch diagnosable from the log line the publisher writes.
        var body = await response.Content.ReadAsStringAsync(ct);

        throw new HttpRequestException(
            $"M-10 rejected the data-scope parameter batch with {(int)response.StatusCode}: {body}");
    }
}
