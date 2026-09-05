using Nabadat.IntegrationHub.Application.Parameters.Dtos;

namespace Nabadat.IntegrationHub.Application.Parameters.Interfaces;

/// <summary>
/// The transport seam for M-13's <b>real</b> outbound call to M-10's
/// <c>POST /api/v1/authorization/scope/parameters</c> (research.md §4.1, CMC-06). Implemented by
/// <c>DataScopeHttpClient</c> in <c>Infrastructure/UserManagementIntegration/</c>.
///
/// <para>It exists so <see cref="DataScopeContractPublisher"/> — which owns the <i>policy</i> (which parameters
/// qualify, reserved-name filtering, batching) — stays in the Application layer with no <c>HttpClient</c>
/// dependency, and so a unit test can substitute the transport without a server.</para>
/// </summary>
public interface IDataScopeContractClient
{
    /// <summary>
    /// Pushes one batch. Throws on a transport failure or a non-2xx response — the caller
    /// (<see cref="DataScopeContractPublisher"/>) decides whether that is fatal.
    /// </summary>
    Task PublishAsync(DataScopeContractPayload payload, CancellationToken ct = default);
}
