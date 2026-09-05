using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.KpiTypes.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Application.KpiBindings;

/// <summary>
/// Default <see cref="IKpiWeightValidator"/> (T045 / US-2). Enforces the touchpoint KPI weight rules
/// from <c>contracts/configuration-api.md §PUT /api/v1/touchpoints/{id}/kpis</c> using
/// <see cref="decimal"/> arithmetic throughout (the sum target is <c>100.00m</c> — <see cref="double"/>
/// would accumulate representation error and risk a spurious sum rejection). A binding's
/// <c>kpiType</c> is "known" when it appears in the active bindable catalogue
/// (<see cref="IActiveKpiCatalogReader"/>) — in the deployed host that is the tenant's active
/// KPI-Management catalogue (M-06); standalone it is M-16's platform-standard + tenant-defined types.
/// </summary>
public sealed class KpiWeightValidator : IKpiWeightValidator
{
    /// <summary>A non-empty binding set's weights must sum to exactly this (decimal, not double).</summary>
    private const decimal RequiredWeightSum = 100.00m;

    private readonly IActiveKpiCatalogReader _catalog;

    public KpiWeightValidator(IActiveKpiCatalogReader catalog) => _catalog = catalog;

    /// <inheritdoc />
    public async Task<ServiceResult> ValidateAsync(
        IReadOnlyList<KpiBindingInput> bindings,
        CancellationToken ct = default)
    {
        // An empty set saves an unmeasured touchpoint (all existing bindings deleted) — valid.
        if (bindings.Count == 0)
            return ServiceResult.Success();

        // Rule: every weight must sit in (0, 100].
        foreach (var binding in bindings)
        {
            if (binding.Weight <= 0m || binding.Weight > 100m)
                return ServiceResult.Failure(
                    "kpi.individual_weight_invalid",
                    $"KPI weight must be greater than 0 and at most 100; '{binding.KpiType}' has weight {binding.Weight}.");
        }

        // Rule: no duplicate kpiType in a single request.
        var seenTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in bindings)
        {
            if (!seenTypes.Add(binding.KpiType))
                return ServiceResult.Failure(
                    "kpi.duplicate_type",
                    $"KPI type '{binding.KpiType}' appears more than once.");
        }

        // Rule: each kpiType must be a currently-bindable KPI (an active KPI-Management catalogue key).
        var knownKeys = (await _catalog.GetActiveKpisAsync(ct))
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var binding in bindings)
        {
            if (!knownKeys.Contains(binding.KpiType))
                return ServiceResult.Failure(
                    "kpi.unknown_type",
                    $"KPI type '{binding.KpiType}' is not an active KPI for this tenant.");
        }

        // Rule: weights must sum to exactly 100.00 — decimal arithmetic avoids the representation
        // drift that would make e.g. 33.34 + 33.33 + 33.33 miss 100 under double.
        var weightSum = bindings.Sum(binding => binding.Weight);
        if (weightSum != RequiredWeightSum)
            return ServiceResult.Failure(
                "kpi.weight_sum_invalid",
                $"KPI weights must sum to exactly 100; got {weightSum}.");

        return ServiceResult.Success();
    }
}
