using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Nabadat.CustomerJourneyManagement.Application.Bindings.Dtos;
using Nabadat.CustomerJourneyManagement.Application.Bindings.Interfaces;
using Nabadat.KpiManagement.Application.Cxi.Interfaces;
using Nabadat.KpiManagement.Application.Events;
using Nabadat.KpiManagement.Application.Interfaces;
using Nabadat.KpiManagement.Application.Kpis.Dtos;
using Nabadat.KpiManagement.Application.Kpis.Interfaces;
using Nabadat.KpiManagement.Application.Kpis.Services;
using Nabadat.KpiManagement.Domain.Entities;
using Nabadat.KpiManagement.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nabadat.KpiManagement.UnitTests.Kpis;

/// <summary>
/// T117 [US5] — unit tests for <c>KpiActivationCommandHandler</c> (the FR-026 activate/deactivate
/// orchestrator with binding-aware confirmation + CXI cascade), covering the spec.md US-5 Required
/// cases: deactivating a bound KPI without confirmation returns the binding-usage counts and writes
/// nothing; deactivating with <c>confirm:true</c> persists, applies the CXI side-effects, and emits
/// exactly ONE <c>settings.changed</c> event carrying the nested <c>cxi_side_effect</c>; reactivating
/// a previously inactive KPI persists <c>Active=true</c> and never touches journey bindings or CXI
/// weights (it does NOT re-create M-16 bindings).
/// <para>
/// Contract pinned for the implementer (T121):
/// <list type="bullet">
///   <item>ctor <c>KpiActivationCommandHandler(ITenantDbContext context, IKpiDefinitionService definitions,
///   ICxiWeightService cxiWeights, KpiBindingUsageProbe bindingUsage, KpiEventPublisher events,
///   TimeProvider timeProvider)</c> — depends on the per-entity service <em>ports</em> (the DB-08
///   mock seam), the concrete M-06 <c>KpiBindingUsageProbe</c> (wrapping M-16's read-only
///   <see cref="IJourneyBindingQuery"/>) and <c>KpiEventPublisher</c>, and an injected <see cref="TimeProvider"/>.</item>
///   <item><c>Task&lt;KpiActivationResult&gt; HandleAsync(KpiActivationCommand command, CancellationToken ct = default)</c>.</item>
///   <item><c>KpiActivationCommand(Guid KpiId, bool Active, bool Confirm, Guid ActorId, string ActorPersona, Guid CorrelationId)</c>.</item>
///   <item><c>KpiActivationResult</c> with an <c>Outcome</c> of <see cref="KpiActivationOutcome.Persisted"/> or
///   <see cref="KpiActivationOutcome.RequiresConfirmation"/> plus the <c>TouchpointCount</c>/<c>JourneyCount</c>
///   binding-usage counts (0 unless RequiresConfirmation); factory members
///   <c>KpiActivationResult.Persisted()</c> / <c>KpiActivationResult.RequiresConfirmation(int, int)</c>.</item>
///   <item>Deactivation requires confirmation ONLY when the KPI is bound (counts &gt; 0); <c>confirm:true</c>
///   bypasses the probe. The deactivation write (KPI update + per-CXI <c>RemoveMemberAsync</c> + the audit
///   row) runs inside one <c>ITenantDbContext.ExecuteAsync</c> tx; the audit's <c>occurredAtUtc</c> comes
///   from <c>timeProvider.GetUtcNow()</c> and the diff carries the <c>deactivated</c> action token plus the
///   nested <c>cxi_side_effect</c> payload.</item>
///   <item>Activation (<c>active:true</c>) is a pure KPI-state flip: it never probes nor mutates M-16
///   journey bindings and never touches <c>cxi_weights</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class KpiActivationCommandHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid Nps = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Csat = Guid.Parse("00000000-0000-0000-0000-0000000000a2");

    private readonly ITenantDbContext _context = Substitute.For<ITenantDbContext>();
    private readonly DbSet<EventLog> _eventLogs = Substitute.For<DbSet<EventLog>>();
    private readonly IKpiDefinitionService _definitions = Substitute.For<IKpiDefinitionService>();
    private readonly ICxiWeightService _cxiWeights = Substitute.For<ICxiWeightService>();
    private readonly IJourneyBindingQuery _bindings = Substitute.For<IJourneyBindingQuery>();
    private readonly FakeTimeProvider _time = new(FixedNow);
    private readonly List<EventLog> _capturedEvents = [];

    public KpiActivationCommandHandlerTests()
    {
        _context.EventLogs.Returns(_eventLogs);
        _context.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        // ExecuteAsync runs the supplied unit of work inline (the real tx wrapper just commits it).
        _context.ExecuteAsync(Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((Func<Task>)ci[0]).Invoke());
        _eventLogs.Add(Arg.Do<EventLog>(e => _capturedEvents.Add(e)));
        // Default: KPI is in no CXI (each cascade test overrides).
        _cxiWeights.GetCxiMembershipsForKpiAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private KpiActivationCommandHandler CreateHandler() =>
        new(_context, _definitions, _cxiWeights, new KpiBindingUsageProbe(_bindings), new KpiEventPublisher(_context), _time);

    [Fact]
    public async Task HandleAsync_returns_requires_confirmation_with_binding_counts_and_writes_nothing_when_deactivating_a_bound_kpi_without_confirm()
    {
        var kpi = ActiveKpi();
        _definitions.GetByIdAsync(kpi.Id, Arg.Any<CancellationToken>()).Returns(kpi);
        _bindings.GetKpiBindingUsageAsync(kpi.Id, Arg.Any<CancellationToken>()).Returns(new KpiBindingUsage(3, 2));

        var result = await CreateHandler().HandleAsync(
            new KpiActivationCommand(kpi.Id, Active: false, Confirm: false, Guid.NewGuid(), "P-01", Guid.NewGuid()));

        result.Outcome.Should().Be(KpiActivationOutcome.RequiresConfirmation);
        result.TouchpointCount.Should().Be(3);
        result.JourneyCount.Should().Be(2);
        await _definitions.DidNotReceive().UpdateAsync(Arg.Any<KpiDefinition>(), Arg.Any<CancellationToken>());
        await _cxiWeights.DidNotReceive().RemoveMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        _eventLogs.DidNotReceive().Add(Arg.Any<EventLog>());
    }

    [Fact]
    public async Task HandleAsync_persists_and_applies_side_effects_and_emits_one_event_with_cxi_side_effect_when_deactivation_confirmed()
    {
        var kpi = ActiveKpi();
        kpi.ShowOnDashboard = true;
        _definitions.GetByIdAsync(kpi.Id, Arg.Any<CancellationToken>()).Returns(kpi);
        _bindings.GetKpiBindingUsageAsync(kpi.Id, Arg.Any<CancellationToken>()).Returns(new KpiBindingUsage(3, 2));

        var cxi = Guid.NewGuid();
        _cxiWeights.GetCxiMembershipsForKpiAsync(kpi.Id, Arg.Any<CancellationToken>())
            .Returns([Weight(cxi, kpi.Id, 2)]);
        _cxiWeights.ListByCxiKpiIdAsync(cxi, Arg.Any<CancellationToken>())
            .Returns([Weight(cxi, kpi.Id, 2), Weight(cxi, Nps, 3), Weight(cxi, Csat, 5)]);

        var result = await CreateHandler().HandleAsync(
            new KpiActivationCommand(kpi.Id, Active: false, Confirm: true, Guid.NewGuid(), "P-01", Guid.NewGuid()));

        result.Outcome.Should().Be(KpiActivationOutcome.Persisted);
        await _definitions.Received(1).UpdateAsync(
            Arg.Is<KpiDefinition>(k => k.Id == kpi.Id && !k.IsActive && !k.ShowOnDashboard), Arg.Any<CancellationToken>());
        await _cxiWeights.Received(1).RemoveMemberAsync(cxi, kpi.Id, Arg.Any<CancellationToken>());
        _capturedEvents.Should().ContainSingle();
        _capturedEvents[0].NewValue.Should().Contain("deactivated");
        _capturedEvents[0].NewValue.Should().Contain("cxi_side_effect");
        _capturedEvents[0].OccurredAtUtc.Should().Be(FixedNow);
    }

    [Fact]
    public async Task HandleAsync_activates_an_inactive_kpi_without_touching_journey_bindings_or_cxi_weights()
    {
        var kpi = ActiveKpi();
        kpi.IsActive = false;
        _definitions.GetByIdAsync(kpi.Id, Arg.Any<CancellationToken>()).Returns(kpi);

        var result = await CreateHandler().HandleAsync(
            new KpiActivationCommand(kpi.Id, Active: true, Confirm: false, Guid.NewGuid(), "P-01", Guid.NewGuid()));

        result.Outcome.Should().Be(KpiActivationOutcome.Persisted);
        await _definitions.Received(1).UpdateAsync(
            Arg.Is<KpiDefinition>(k => k.Id == kpi.Id && k.IsActive), Arg.Any<CancellationToken>());
        // Activation never re-creates nor probes M-16 bindings, and never mutates CXI weights.
        await _bindings.DidNotReceive().GetKpiBindingUsageAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _cxiWeights.DidNotReceive().RemoveMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _cxiWeights.DidNotReceive()
            .ReplaceAllAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<CxiWeight>>(), Arg.Any<CancellationToken>());
    }

    private static KpiDefinition ActiveKpi() => new()
    {
        Id = Guid.NewGuid(),
        ShortName = "QUAL",
        FullName = "Service Quality",
        KpiType = KpiType.Custom,
        IsComposite = false,
        CalculationMethod = CalculationMethod.WeightedAverage,
        Scale = Scale.Scale1_5,
        RepresentationStyle = RepresentationStyle.Number,
        Target = 80m,
        IsActive = true,
    };

    private static CxiWeight Weight(Guid cxiKpiId, Guid memberKpiId, int weight) =>
        new() { CxiKpiId = cxiKpiId, MemberKpiId = memberKpiId, Weight = (short)weight };
}
