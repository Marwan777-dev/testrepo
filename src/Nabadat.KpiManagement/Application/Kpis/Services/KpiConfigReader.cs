using Microsoft.EntityFrameworkCore;
using Nabadat.KpiManagement.Application.Cxi;
using Nabadat.KpiManagement.Application.Interfaces;
using Nabadat.KpiManagement.Application.Kpis.Dtos;
using Nabadat.KpiManagement.Application.Kpis.Interfaces;
using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Application.Kpis.Services;

/// <summary>
/// The published read contract (<see cref="IKpiConfigReader"/>): M-06 → M-01 / M-07 / M-09
/// (AD-01 / AMENDMENT-006). It assembles the published DTOs from <c>kpi_definitions</c> plus the
/// joined thresholds / perspectives / cxi_weights; reads use <c>AsNoTracking</c>. Effective CXI
/// percentages are derived here from the relative weights (sum→100); the live CXI composite and
/// per-member scores depend on the score-computation engine (out of scope for this feature) and are
/// returned as 0 placeholders.
/// <para>This is the read surface only — it holds no write methods (those live on
/// <c>KpiSaveService</c>) — and is a distinct class from the internal entity service
/// <see cref="KpiDefinitionService"/> (<see cref="IKpiDefinitionService"/>), which returns entities.</para>
/// </summary>
public sealed class KpiConfigReader : IKpiConfigReader
{
    private readonly ITenantDbContext _context;

    public KpiConfigReader(ITenantDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<IReadOnlyList<KpiDefinitionDto>> GetActiveAsync(CancellationToken ct = default)
    {
        var definitions = await KpiCatalogueQuery
            .Build(_context.KpiDefinitions.AsNoTracking(), KpiTypeFilter.All, activeOnly: true, search: null)
            .ToListAsync(ct);

        if (definitions.Count == 0)
        {
            return [];
        }

        var ids = definitions.Select(d => d.Id).ToHashSet();
        var thresholds = await _context.KpiThresholds.AsNoTracking().Where(t => ids.Contains(t.KpiId)).ToListAsync(ct);
        var perspectives = await _context.KpiPerspectives.AsNoTracking().Where(p => ids.Contains(p.KpiId)).ToListAsync(ct);
        var weights = await _context.CxiWeights.AsNoTracking().Where(w => ids.Contains(w.CxiKpiId)).ToListAsync(ct);
        var memberNames = await ResolveMemberNamesAsync(weights, ct);

        return definitions
            .Select(d => Assemble(d, thresholds, perspectives, weights, memberNames))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<KpiDefinitionDto?> GetByIdAsync(Guid kpiId, CancellationToken ct = default)
    {
        var definition = await _context.KpiDefinitions.AsNoTracking().FirstOrDefaultAsync(k => k.Id == kpiId, ct);
        return definition is null ? null : await AssembleSingleAsync(definition, ct);
    }

    /// <inheritdoc />
    public async Task<KpiDefinitionDto?> GetByShortNameAsync(string shortName, CancellationToken ct = default)
    {
        var lowered = (shortName ?? string.Empty).ToLower();
        var definition = await _context.KpiDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(k => k.ShortName.ToLower() == lowered, ct);
        return definition is null ? null : await AssembleSingleAsync(definition, ct);
    }

    /// <inheritdoc />
    public async Task<CxiSnapshotDto?> GetCxiSnapshotAsync(CancellationToken ct = default)
    {
        var cxi = await _context.KpiDefinitions.AsNoTracking().FirstOrDefaultAsync(k => k.IsComposite, ct);
        if (cxi is null || !cxi.IsActive)
        {
            return null;
        }

        var weights = await _context.CxiWeights.AsNoTracking().Where(w => w.CxiKpiId == cxi.Id).ToListAsync(ct);
        if (weights.Count < 2)
        {
            return null;
        }

        var memberNames = await ResolveMemberNamesAsync(weights, ct);

        // Effective percentages + the snapshot shape are assembled by CxiSnapshotComposer (T086 /
        // CxiWeightNormaliser). Per-member scores and the live composite score require the
        // score-computation engine (out of scope here), so they are passed as 0 placeholders.
        var members = weights
            .Select(w => new CxiMemberInput(
                w.MemberKpiId,
                memberNames.GetValueOrDefault(w.MemberKpiId, string.Empty),
                w.Weight,
                Score: 0m))
            .ToList();

        return CxiSnapshotComposer.Compose(compositeScore: 0m, members);
    }

    private async Task<KpiDefinitionDto> AssembleSingleAsync(KpiDefinition definition, CancellationToken ct)
    {
        var thresholds = await _context.KpiThresholds.AsNoTracking().Where(t => t.KpiId == definition.Id).ToListAsync(ct);
        var perspectives = await _context.KpiPerspectives.AsNoTracking().Where(p => p.KpiId == definition.Id).ToListAsync(ct);
        var weights = definition.IsComposite
            ? await _context.CxiWeights.AsNoTracking().Where(w => w.CxiKpiId == definition.Id).ToListAsync(ct)
            : [];
        var memberNames = await ResolveMemberNamesAsync(weights, ct);

        return Assemble(definition, thresholds, perspectives, weights, memberNames);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveMemberNamesAsync(
        IReadOnlyList<CxiWeight> weights,
        CancellationToken ct)
    {
        if (weights.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var memberIds = weights.Select(w => w.MemberKpiId).ToHashSet();
        return await _context.KpiDefinitions.AsNoTracking()
            .Where(k => memberIds.Contains(k.Id))
            .ToDictionaryAsync(k => k.Id, k => k.ShortName, ct);
    }

    private static KpiDefinitionDto Assemble(
        KpiDefinition d,
        IReadOnlyList<KpiThreshold> thresholds,
        IReadOnlyList<KpiPerspective> perspectives,
        IReadOnlyList<CxiWeight> weights,
        IReadOnlyDictionary<Guid, string> memberNames)
    {
        var threshold = thresholds.FirstOrDefault(t => t.KpiId == d.Id);
        var thresholdDto = threshold is null
            ? new KpiThresholdDto(0m, 0m, 0m, 0m)
            : new KpiThresholdDto(threshold.LowerBound, threshold.X, threshold.Y, threshold.UpperBound);

        var perspectiveDtos = perspectives
            .Where(p => p.KpiId == d.Id)
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new KpiPerspectiveDto(p.Id, p.Label, p.DisplayOrder))
            .ToList();

        IReadOnlyList<CxiWeightDto>? cxiWeightDtos = null;
        if (d.IsComposite)
        {
            var rows = weights.Where(w => w.CxiKpiId == d.Id).ToList();
            var total = rows.Sum(w => (int)w.Weight);
            cxiWeightDtos = rows
                .Select(w => new CxiWeightDto(
                    w.MemberKpiId,
                    memberNames.GetValueOrDefault(w.MemberKpiId, string.Empty),
                    w.Weight,
                    EffectivePercentage(w.Weight, total)))
                .ToList();
        }

        return new KpiDefinitionDto(
            d.Id,
            d.ShortName,
            d.FullName,
            d.KpiType,
            d.IsComposite,
            d.CalculationMethod,
            d.TopNValue,
            d.Scale,
            Bilingual(d.MinScaleDescriptionEn, d.MinScaleDescriptionAr),
            Bilingual(d.MaxScaleDescriptionEn, d.MaxScaleDescriptionAr),
            d.RepresentationStyle,
            d.EmojiSet,
            d.Target,
            d.IsActive,
            d.ShowOnDashboard,
            thresholdDto,
            perspectiveDtos,
            cxiWeightDtos);
    }

    /// <summary>Relative weight → share of the composite (sum→100), rounded to 1 dp; 0 when no weights.</summary>
    private static decimal EffectivePercentage(short weight, int total) =>
        total == 0 ? 0m : Math.Round((decimal)weight / total * 100m, 1, MidpointRounding.AwayFromZero);

    /// <summary>Builds a <see cref="BilingualText"/> when either anchor is present; null when both are absent.</summary>
    private static BilingualText? Bilingual(string? en, string? ar) =>
        en is null && ar is null ? null : new BilingualText(en ?? string.Empty, ar ?? string.Empty);
}
