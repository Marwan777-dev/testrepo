using Nabadat.KpiManagement.Application.Cxi.Interfaces;
using Nabadat.KpiManagement.Application.Events;
using Nabadat.KpiManagement.Application.Interfaces;
using Nabadat.KpiManagement.Application.Kpis.Dtos;
using Nabadat.KpiManagement.Application.Kpis.Interfaces;
using Nabadat.KpiManagement.Domain.Entities;

namespace Nabadat.KpiManagement.Application.Kpis.Services;

/// <summary>
/// T121 [US-5] — orchestrates the FR-026 activate / deactivate flow (per research.md R5).
/// <list type="bullet">
///   <item><b>Activate</b> (<c>Active=true</c>) is a pure KPI-state flip: it never probes nor mutates
///   M-16 journey bindings and never touches <c>cxi_weights</c>. Idempotent (already-active → no-op).</item>
///   <item><b>Deactivate</b> (<c>Active=false</c>) without <see cref="KpiActivationCommand.Confirm"/>:
///   probes M-16 binding usage and, if the KPI is bound, returns
///   <see cref="KpiActivationOutcome.RequiresConfirmation"/> with the counts and writes nothing.</item>
///   <item><b>Deactivate confirmed</b> (or an unbound KPI): inside one
///   <see cref="ITenantDbContext.ExecuteAsync(Func{Task}, CancellationToken)"/> transaction it sets
///   <c>is_active=false</c>, forces <c>show_on_dashboard=false</c>, removes the KPI from every CXI it
///   belonged to, and appends exactly ONE <c>settings.changed</c> event whose diff carries the
///   <c>deactivated</c> action and the nested <c>cxi_side_effect</c> payload. The cascade maths is the
///   shared pure function <see cref="KpiDeactivationSideEffects"/>.</item>
/// </list>
/// Time is injected (<see cref="TimeProvider"/>); the audit row commits with the KPI write because the
/// publisher shares the same context inside the transaction (data-model.md §8).
/// </summary>
public sealed class KpiActivationCommandHandler
{
    private readonly ITenantDbContext _context;
    private readonly IKpiDefinitionService _definitions;
    private readonly ICxiWeightService _cxiWeights;
    private readonly KpiBindingUsageProbe _bindingUsage;
    private readonly KpiEventPublisher _events;
    private readonly TimeProvider _timeProvider;

    public KpiActivationCommandHandler(
        ITenantDbContext context,
        IKpiDefinitionService definitions,
        ICxiWeightService cxiWeights,
        KpiBindingUsageProbe bindingUsage,
        KpiEventPublisher events,
        TimeProvider timeProvider)
    {
        _context = context;
        _definitions = definitions;
        _cxiWeights = cxiWeights;
        _bindingUsage = bindingUsage;
        _events = events;
        _timeProvider = timeProvider;
    }

    /// <summary>Activates or deactivates the KPI; see the type summary for the per-path behaviour.</summary>
    public async Task<KpiActivationResult> HandleAsync(KpiActivationCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var kpi = await _definitions.GetByIdAsync(command.KpiId, ct);
        if (kpi is null)
        {
            return KpiActivationResult.NotFound();
        }

        if (command.Active)
        {
            // Idempotent: already active → nothing to do, no binding/CXI interaction whatsoever.
            return kpi.IsActive
                ? KpiActivationResult.Persisted()
                : await PersistActivationAsync(kpi, command, ct);
        }

        // Deactivation. Idempotent when already inactive.
        if (!kpi.IsActive)
        {
            return KpiActivationResult.Persisted();
        }

        // Binding-aware confirmation gate (FR-026): a bound KPI needs explicit confirmation.
        if (!command.Confirm)
        {
            var (touchpoints, journeys) = await _bindingUsage.GetUsageAsync(kpi.Id, ct);
            if (touchpoints > 0 || journeys > 0)
            {
                return KpiActivationResult.RequiresConfirmation(touchpoints, journeys);
            }
        }

        return await PersistDeactivationAsync(kpi, command, ct);
    }

    private async Task<KpiActivationResult> PersistActivationAsync(
        KpiDefinition kpi, KpiActivationCommand command, CancellationToken ct)
    {
        var occurredAt = _timeProvider.GetUtcNow();

        await _context.ExecuteAsync(async () =>
        {
            kpi.IsActive = true;
            kpi.UpdatedAt = occurredAt;
            kpi.UpdatedBy = command.ActorId;
            await _definitions.UpdateAsync(kpi, ct);

            var oldValue = new { is_active = false };
            var newValue = new
            {
                action = "activated",
                diff = new { is_active = new { from = false, to = true } },
            };

            await _events.PublishKpiSettingsChangedAsync(
                kpi.Id, command.ActorId, command.ActorPersona, oldValue, newValue, occurredAt, command.CorrelationId, ct);
        }, ct);

        return KpiActivationResult.Persisted();
    }

    private async Task<KpiActivationResult> PersistDeactivationAsync(
        KpiDefinition kpi, KpiActivationCommand command, CancellationToken ct)
    {
        var occurredAt = _timeProvider.GetUtcNow();
        var wasShownOnDashboard = kpi.ShowOnDashboard;

        // Load the full membership of every CXI that lists this KPI, for the recompute.
        var memberships = await _cxiWeights.GetCxiMembershipsForKpiAsync(kpi.Id, ct);
        var affected = new List<CxiWeight>();
        foreach (var cxiId in memberships.Select(m => m.CxiKpiId).Distinct())
        {
            affected.AddRange(await _cxiWeights.ListByCxiKpiIdAsync(cxiId, ct));
        }

        var plan = KpiDeactivationSideEffects.Compute(kpi, affected);

        await _context.ExecuteAsync(async () =>
        {
            kpi.IsActive = false;
            kpi.ShowOnDashboard = plan.ShowOnDashboard; // forced false
            kpi.UpdatedAt = occurredAt;
            kpi.UpdatedBy = command.ActorId;
            await _definitions.UpdateAsync(kpi, ct);

            foreach (var effect in plan.CxiSideEffects)
            {
                await _cxiWeights.RemoveMemberAsync(effect.CxiKpiId, effect.RemovedMemberKpiId, ct);
            }

            var oldValue = new { is_active = true, show_on_dashboard = wasShownOnDashboard };
            var newValue = new
            {
                action = "deactivated",
                diff = new
                {
                    is_active = new { from = true, to = false },
                    show_on_dashboard = new { from = wasShownOnDashboard, to = false },
                },
                cxi_side_effect = plan.CxiSideEffects
                    .Select(e => new
                    {
                        cxi_kpi_id = e.CxiKpiId,
                        removed_member_kpi_id = e.RemovedMemberKpiId,
                        effective_percentages = e.RecomputedEffectivePercentages
                            .Select(p => new { member_kpi_id = p.MemberKpiId, effective_percentage = p.EffectivePercentage })
                            .ToArray(),
                    })
                    .ToArray(),
            };

            await _events.PublishKpiSettingsChangedAsync(
                kpi.Id, command.ActorId, command.ActorPersona, oldValue, newValue, occurredAt, command.CorrelationId, ct);
        }, ct);

        return KpiActivationResult.Persisted();
    }
}
