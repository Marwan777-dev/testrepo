using Microsoft.EntityFrameworkCore;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Application.KpiTypes;

/// <summary>
/// EF <see cref="IKpiTypeDataService"/> over <see cref="ITenantDbContext"/> for the tenant-schema
/// <c>kpi_type_definitions</c> table. Replaces the raw-Npgsql <c>KpiTypeRepository</c>. The six
/// platform-standard KPI types are built into the platform and NOT stored here; this service backs
/// only unknown-type resolution and the <c>kpi_type.key_conflict</c> guard on create.
/// </summary>
public sealed class KpiTypeDataService : IKpiTypeDataService
{
    private readonly ITenantDbContext _context;

    public KpiTypeDataService(ITenantDbContext context) => _context = context;

    /// <inheritdoc />
    public Task<KpiTypeDefinition?> GetByKeyAsync(string typeKey, CancellationToken ct = default) =>
        _context.KpiTypeDefinitions.AsNoTracking().FirstOrDefaultAsync(k => k.TypeKey == typeKey, ct);

    /// <inheritdoc />
    public Task<bool> ExistsByKeyAsync(string typeKey, CancellationToken ct = default) =>
        _context.KpiTypeDefinitions.AsNoTracking().AnyAsync(k => k.TypeKey == typeKey, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<KpiTypeDefinition>> ListAsync(CancellationToken ct = default) =>
        await _context.KpiTypeDefinitions.AsNoTracking().OrderBy(k => k.TypeKey).ToListAsync(ct);

    /// <inheritdoc />
    public async Task CreateAsync(KpiTypeDefinition definition, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _context.KpiTypeDefinitions.Add(definition);
        await _context.SaveChangesAsync(ct);
    }
}
