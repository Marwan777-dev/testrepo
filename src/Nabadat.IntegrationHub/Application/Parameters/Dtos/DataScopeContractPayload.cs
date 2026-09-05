namespace Nabadat.IntegrationHub.Application.Parameters.Dtos;

/// <summary>
/// One batch pushed to M-10's <c>POST /api/v1/authorization/scope/parameters</c> — the wire shape of
/// <c>M13ParameterPayload</c> (research.md §4.1).
/// </summary>
/// <param name="SourceModule">Identifies M-13 as the provider; M-10 stores it on each definition.</param>
/// <param name="Parameters">At most <c>DataScopeContractPublisher.MaxDefinitionsPerPayload</c> definitions — M-10 rejects a larger batch outright.</param>
public sealed record DataScopeContractPayload(
    string SourceModule,
    IReadOnlyList<DataScopeParameterContract> Parameters);
