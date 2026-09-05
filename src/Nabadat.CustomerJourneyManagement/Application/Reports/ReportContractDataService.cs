using Microsoft.EntityFrameworkCore;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Application.Reports;

/// <summary>
/// EF <see cref="IReportContractDataService"/> over <see cref="ITenantDbContext"/> for the
/// tenant-schema <c>report_contracts</c> table (one row per journey). Replaces the raw-Npgsql
/// <c>ReportContractRepository</c>; the old <c>INSERT … ON CONFLICT (journey_id) DO UPDATE</c>
/// upsert becomes a load-or-add (preserving the original <c>report_contract_id</c> /
/// <c>created_at</c>). The opaque M-07 <c>contract_payload</c> jsonb is stored verbatim and read
/// back through the published <c>IReportContractReader</c>.
/// </summary>
public sealed class ReportContractDataService : IReportContractDataService
{
    private readonly ITenantDbContext _context;

    public ReportContractDataService(ITenantDbContext context) => _context = context;

    /// <inheritdoc />
    public Task<ReportContract?> GetByJourneyAsync(Guid journeyId, CancellationToken ct = default) =>
        _context.ReportContracts.AsNoTracking().FirstOrDefaultAsync(c => c.JourneyId == journeyId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReportContract>> ListByActiveJourneysAsync(CancellationToken ct = default) =>
        await _context.ReportContracts.AsNoTracking()
            .Where(rc => _context.Journeys.Any(j => j.JourneyId == rc.JourneyId && j.Status == "Active"))
            .OrderBy(rc => rc.JourneyId)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task UpsertAsync(ReportContract contract, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var existing = await _context.ReportContracts.FirstOrDefaultAsync(c => c.JourneyId == contract.JourneyId, ct);
        if (existing is null)
        {
            _context.ReportContracts.Add(contract);
        }
        else
        {
            // Replace in place — preserve the original report_contract_id and created_at.
            existing.ContractPayload = contract.ContractPayload;
            existing.GeneratedAt = contract.GeneratedAt;
            existing.UpdatedAt = contract.UpdatedAt;
        }

        await _context.SaveChangesAsync(ct);
    }
}
