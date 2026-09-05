using Microsoft.EntityFrameworkCore;
using Nabadat.Platform.Contracts.M16;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Application.Scoring;

/// <summary>
/// M-16's published <see cref="IJourneyConfigReader"/> implementation (T049 / US-2): the
/// in-process read M-06 calls to fetch the journey configuration it needs for score
/// computation.
/// <list type="bullet">
///   <item><description>
///     <b>Direct EF read, no cache.</b> Per <c>contracts/published-interfaces.md</c> the
///     service queries the tenant schema through <see cref="ITenantDbContext"/> and constructs
///     the DTO fresh on every call (no cross-request caching). Replaces the raw-Npgsql reader;
///     all reads are <c>AsNoTracking</c>.
///   </description></item>
///   <item><description>
///     <b>Unmeasured touchpoints are included</b> (FR-008 / contract rule 3) with
///     <see cref="TouchpointConfigDto.IsMeasured"/> = <see langword="false"/> and an empty
///     <see cref="TouchpointConfigDto.KpiBindings"/>; M-06 excludes them from computation.
///   </description></item>
///   <item><description>
///     <b>Scoring is no longer journey-scoped</b> (SRS §4.2.9 / §11.7, Q11): the journey config carries
///     only KPI bindings + structure. M-06 reads the tenant scoring parameters separately via
///     <c>IScoringConfigStore</c>. <see cref="GetJourneyConfigAsync"/> returns <see langword="null"/>
///     only when the journey itself does not exist.
///   </description></item>
///   <item><description>
///     <b>Scoring direction is resolved, not stored.</b> <c>kpi_bindings</c> holds no direction;
///     platform-standard KPIs derive it intrinsically (all <c>Ascending</c> except <c>CES</c>),
///     tenant-defined KPIs read it from <c>kpi_type_definitions.scoring_direction</c>.
///   </description></item>
/// </list>
/// Registered as <c>Scoped</c> — M-06 receives it via constructor injection and never touches
/// M-16 tables directly.
/// </summary>
public sealed class JourneyConfigReaderService : IJourneyConfigReader
{
    /// <summary>The platform-standard KPI key whose scoring direction is <c>Descending</c> (lower is better).</summary>
    private static readonly string DescendingPlatformKpi =
        nameof(Nabadat.CustomerJourneyManagement.Domain.ValueObjects.PlatformKpiType.CES);

    private readonly ITenantDbContext _context;

    public JourneyConfigReaderService(ITenantDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<JourneyConfigDto?> GetJourneyConfigAsync(Guid journeyId, CancellationToken ct = default)
    {
        var configs = await ReadConfigsAsync(journeyId, activeOnly: false, ct);
        return configs.Count > 0 ? configs[0] : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<JourneyConfigDto>> GetActiveJourneyConfigsAsync(CancellationToken ct = default)
        => await ReadConfigsAsync(journeyId: null, activeOnly: true, ct);

    /// <summary>
    /// Reads the journey tree via EF and assembles it. <paramref name="journeyId"/> null +
    /// <paramref name="activeOnly"/> true selects every active journey; a concrete
    /// <paramref name="journeyId"/> selects exactly that journey.
    /// </summary>
    private async Task<List<JourneyConfigDto>> ReadConfigsAsync(
        Guid? journeyId,
        bool activeOnly,
        CancellationToken ct)
    {
        var journeys = await _context.Journeys.AsNoTracking()
            .Where(j => (journeyId == null || j.JourneyId == journeyId)
                && (!activeOnly || j.Status == "Active"))
            .OrderBy(j => j.JourneyId)
            .Select(j => new { j.JourneyId, j.Name, j.Status })
            .ToListAsync(ct);

        // No journey matched the filter — nothing further to read.
        if (journeys.Count == 0)
        {
            return [];
        }

        var journeyIds = journeys.Select(j => j.JourneyId).ToList();

        var stages = await _context.Stages.AsNoTracking()
            .Where(s => journeyIds.Contains(s.JourneyId))
            .OrderBy(s => s.JourneyId).ThenBy(s => s.SequenceNumber)
            .Select(s => new { s.JourneyId, s.StageId, s.SequenceNumber, s.Name })
            .ToListAsync(ct);
        var stageIds = stages.Select(s => s.StageId).ToList();

        var touchpoints = await _context.Touchpoints.AsNoTracking()
            .Where(t => stageIds.Contains(t.StageId))
            .OrderBy(t => t.StageId).ThenBy(t => t.CreatedAt).ThenBy(t => t.TouchpointId)
            .Select(t => new { t.StageId, t.TouchpointId, t.Name, t.IsMot, t.IsMandatory })
            .ToListAsync(ct);
        var touchpointIds = touchpoints.Select(t => t.TouchpointId).ToList();

        var bindingRows = await _context.KpiBindings.AsNoTracking()
            .Where(b => touchpointIds.Contains(b.TouchpointId))
            .OrderBy(b => b.TouchpointId).ThenBy(b => b.KpiType)
            .Select(b => new { b.TouchpointId, b.KpiType, b.Weight, b.IsPlatformStandard })
            .ToListAsync(ct);

        // Tenant-defined KPI scoring directions, resolved in memory (mirrors the old LEFT JOIN).
        var directionByKey = await _context.KpiTypeDefinitions.AsNoTracking()
            .ToDictionaryAsync(d => d.TypeKey, d => d.ScoringDirection, ct);

        var bindingsByTouchpoint = bindingRows
            .GroupBy(b => b.TouchpointId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(b => new KpiBindingConfigDto(
                    b.KpiType,
                    b.Weight,
                    b.IsPlatformStandard,
                    ResolveScoringDirection(
                        b.KpiType,
                        b.IsPlatformStandard,
                        directionByKey.GetValueOrDefault(b.KpiType)))).ToList());

        var touchpointsByStage = touchpoints
            .GroupBy(t => t.StageId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var stagesByJourney = stages
            .GroupBy(s => s.JourneyId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Assemble journey → stages → touchpoints → bindings.
        var result = new List<JourneyConfigDto>(journeys.Count);
        foreach (var journey in journeys)
        {
            var journeyStages = stagesByJourney.GetValueOrDefault(journey.JourneyId) ?? [];
            var stageDtos = new List<StageConfigDto>(journeyStages.Count);
            foreach (var stage in journeyStages)
            {
                var stageTouchpoints = touchpointsByStage.GetValueOrDefault(stage.StageId) ?? [];
                var touchpointDtos = new List<TouchpointConfigDto>(stageTouchpoints.Count);
                foreach (var touchpoint in stageTouchpoints)
                {
                    var bindings = bindingsByTouchpoint.GetValueOrDefault(touchpoint.TouchpointId)
                        ?? (IReadOnlyList<KpiBindingConfigDto>)[];
                    touchpointDtos.Add(new TouchpointConfigDto(
                        touchpoint.TouchpointId,
                        touchpoint.Name,
                        touchpoint.IsMot,
                        touchpoint.IsMandatory,
                        IsMeasured: bindings.Count > 0,
                        bindings));
                }

                stageDtos.Add(new StageConfigDto(stage.StageId, stage.SequenceNumber, stage.Name, touchpointDtos));
            }

            result.Add(new JourneyConfigDto(
                journey.JourneyId,
                journey.Name,
                ParseStatus(journey.Status),
                stageDtos));
        }

        return result;
    }

    /// <summary>Maps the stored status string to the enum, defaulting to the safest member when unrecognised.</summary>
    private static JourneyConfigStatus ParseStatus(string status)
        => Enum.TryParse<JourneyConfigStatus>(status, ignoreCase: true, out var parsed)
            ? parsed
            : JourneyConfigStatus.Draft;

    /// <summary>
    /// Resolves a binding's scoring direction. <c>kpi_bindings</c> stores no direction:
    /// platform-standard KPIs derive it (all <c>Ascending</c> except <c>CES</c>); tenant-defined
    /// KPIs read it from <c>kpi_type_definitions.scoring_direction</c>, defaulting to
    /// <see cref="ScoringDirection.Ascending"/> when absent or unrecognised.
    /// </summary>
    private static ScoringDirection ResolveScoringDirection(
        string kpiType,
        bool isPlatformStandard,
        string? definitionDirection)
    {
        if (isPlatformStandard)
        {
            return string.Equals(kpiType, DescendingPlatformKpi, StringComparison.OrdinalIgnoreCase)
                ? ScoringDirection.Descending
                : ScoringDirection.Ascending;
        }

        return Enum.TryParse<ScoringDirection>(definitionDirection, ignoreCase: true, out var direction)
            ? direction
            : ScoringDirection.Ascending;
    }
}
