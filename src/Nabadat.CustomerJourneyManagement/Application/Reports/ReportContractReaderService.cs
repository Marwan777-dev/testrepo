using System.Text.Json;
using Nabadat.Platform.Contracts.M16;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Application.Reports;

/// <summary>
/// M-16's published <see cref="IReportContractReader"/> implementation (T089 / US-4): the in-process
/// read M-07 calls to fetch a journey's report layout/dimension metadata without touching M-16 tables.
///
/// The contract is a pre-built, opaque JSONB payload (rebuilt transactionally by
/// <c>ReportContractService</c> after any configuration write); this reader merely loads it through
/// <see cref="IReportContractDataService"/> and deserializes it back to <see cref="ReportContractDto"/>
/// (<c>contracts/published-interfaces.md</c> rule 1). Deserialization uses the SAME
/// <see cref="JsonSerializerDefaults.Web"/> (camelCase) options the rebuilder serialized with, so the
/// round-trip is symmetric. <see cref="GetReportContractAsync"/> returns <c>null</c> when the journey
/// has no contract row yet (rule 2 — M-07 skips that journey). Registered as <c>Scoped</c> (rule 4).
/// </summary>
public sealed class ReportContractReaderService : IReportContractReader
{
    /// <summary>Mirror of <c>ReportContractService.PayloadOptions</c> — must match for a symmetric round-trip.</summary>
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly IReportContractDataService _reportContracts;

    public ReportContractReaderService(IReportContractDataService reportContracts)
        => _reportContracts = reportContracts;

    /// <inheritdoc />
    public async Task<ReportContractDto?> GetReportContractAsync(Guid journeyId, CancellationToken ct = default)
    {
        var contract = await _reportContracts.GetByJourneyAsync(journeyId, ct);
        return contract is null ? null : Deserialize(contract);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportContractDto>> GetActiveReportContractsAsync(CancellationToken ct = default)
    {
        var contracts = await _reportContracts.ListByActiveJourneysAsync(ct);
        return contracts.Select(Deserialize).ToList();
    }

    /// <summary>
    /// Deserializes the opaque <c>contract_payload</c> back to its DTO. The payload is always a
    /// serialized object (the rebuilder upserts only a non-null contract), so a <c>null</c> result
    /// signals a corrupt row and is surfaced rather than returned silently.
    /// </summary>
    private static ReportContractDto Deserialize(ReportContract contract)
        => JsonSerializer.Deserialize<ReportContractDto>(contract.ContractPayload, PayloadOptions)
           ?? throw new InvalidOperationException(
               $"report_contracts.contract_payload for journey {contract.JourneyId} deserialized to null.");
}
