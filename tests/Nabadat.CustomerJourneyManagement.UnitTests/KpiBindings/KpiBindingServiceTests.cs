using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.Platform.Contracts.M16;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.KpiBindings;
using Nabadat.CustomerJourneyManagement.Application.KpiTypes;
using Nabadat.CustomerJourneyManagement.Application.KpiTypes.Interfaces;
using Nabadat.CustomerJourneyManagement.Application.Reports;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using NSubstitute;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.UnitTests.KpiBindings;

/// <summary>
/// Unit tests for <see cref="KpiBindingService"/> (T043 / US-2) — the touchpoint KPI-binding
/// full-replace save (<c>contracts/configuration-api.md §PUT /api/v1/touchpoints/{id}/kpis</c>).
/// Authored FIRST (red→green per the Unit Test Policy); they define the contract the T047
/// implementation must satisfy:
/// <list type="bullet">
///   <item><c>KpiBindingService(ITouchpointDataService, IStageDataService, IJourneyDataService,
///   IActiveKpiCatalogReader, IKpiWeightValidator, ITenantDbContext, IM17EventPublisher,
///   ReportContractService, TimeProvider)</c>.</item>
///   <item><c>Task&lt;ServiceResult&lt;SaveKpiBindingsResult&gt;&gt; SaveKpiBindingsAsync(Guid
///   touchpointId, IReadOnlyList&lt;KpiBindingInput&gt; bindings, ActorContext actor,
///   CancellationToken ct = default)</c> — validates weights via <see cref="IKpiWeightValidator"/>
///   first (no DB write on failure), then in one transaction full-replaces the touchpoint's
///   bindings (<c>ITouchpointDataService.ReplaceKpiBindingsAsync</c>), publishes
///   <c>journey.kpi_bindings.updated</c>, and rebuilds the report contract.</item>
///   <item><c>record SaveKpiBindingsResult(Guid TouchpointId, IReadOnlyList&lt;KpiBinding&gt;
///   KpiBindings, bool IsMeasured, bool NpsWarning, DateTimeOffset UpdatedAt)</c> — <c>IsMeasured</c>
///   = non-empty set; <c>NpsWarning</c> = NPS present (non-blocking 200 flag).</item>
/// </list>
/// The KPI weight rules themselves are covered by <c>KpiWeightValidatorTests</c> (T042); here the
/// validator is a substitute defaulting to success, so each case isolates the service's own
/// responsibilities: persistence, the NPS-warning flag, and the Archived-parent guard.
/// </summary>
public sealed class KpiBindingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly ActorContext Actor = new(
        UserId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Persona: "P-01",
        CorrelationId: Guid.Parse("66666666-6666-6666-6666-666666666666"));

    private readonly ITouchpointDataService _touchpoints = Substitute.For<ITouchpointDataService>();
    private readonly IStageDataService _stages = Substitute.For<IStageDataService>();
    private readonly IJourneyDataService _journeys = Substitute.For<IJourneyDataService>();
    private readonly IActiveKpiCatalogReader _catalog = Substitute.For<IActiveKpiCatalogReader>();
    private readonly IKpiWeightValidator _weightValidator = Substitute.For<IKpiWeightValidator>();
    private readonly IM17EventPublisher _events = Substitute.For<IM17EventPublisher>();
    private readonly FakeTimeProvider _time = new(Now);

    public KpiBindingServiceTests()
    {
        // Weight validation is exercised by KpiWeightValidatorTests (T042); default it to success
        // here so these cases isolate the service's persistence / warning / guard behaviour.
        _weightValidator
            .ValidateAsync(Arg.Any<IReadOnlyList<KpiBindingInput>>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult.Success());

        // The save path resolves each binding's kpi_id + platform-standard flag from the catalogue;
        // expose the platform-standard keys these cases bind (NPS/CSAT/CES) with a stable id.
        _catalog.GetActiveKpisAsync(Arg.Any<CancellationToken>()).Returns(
            KpiTypeService.PlatformStandardCatalog
                .Select(type => new ActiveKpiCatalogEntry(
                    Guid.NewGuid(), type.TypeKey, type.LabelAr, type.LabelEn, type.ScoringDirection, true))
                .ToList());
    }

    private KpiBindingService CreateSut() => new(
        _touchpoints,
        _stages,
        _journeys,
        _catalog,
        _weightValidator,
        TestSupport.FakeTenantDb.Immediate(),
        _events,
        // The report-contract rebuild (T087) is exercised by its own ReportContractServiceTests;
        // here the real (sealed) service is wired with substitute deps whose journey-config read
        // returns null, so RebuildContractAsync no-ops — these cases isolate KpiBindingService.
        new ReportContractService(
            Substitute.For<IJourneyConfigReader>(),
            Substitute.For<IReportContractDataService>(),
            Substitute.For<IDetectionDataService>(),
            _time),
        _time);

    /// <summary>Wires the touchpoint → stage → journey chain with the given parent-journey status.</summary>
    private Guid GivenTouchpointInJourney(string journeyStatus)
    {
        var touchpointId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var journeyId = Guid.NewGuid();
        _touchpoints.GetByIdAsync(touchpointId, Arg.Any<CancellationToken>())
            .Returns(new Touchpoint { TouchpointId = touchpointId, StageId = stageId, Name = "Checkout" });
        _stages.GetByIdAsync(stageId, Arg.Any<CancellationToken>())
            .Returns(new Stage { StageId = stageId, JourneyId = journeyId, SequenceNumber = 1, Name = "Purchase" });
        _journeys.GetByIdAsync(journeyId, Arg.Any<CancellationToken>())
            .Returns(new Journey { JourneyId = journeyId, Name = "J", Status = journeyStatus });
        return touchpointId;
    }

    [Fact]
    public async Task SaveKpiBindingsAsync_persists_full_binding_set_and_publishes_event_when_weights_are_valid()
    {
        var touchpointId = GivenTouchpointInJourney("Draft");
        var bindings = new[] { new KpiBindingInput("NPS", 60.00m), new KpiBindingInput("CSAT", 40.00m) };

        var result = await CreateSut().SaveKpiBindingsAsync(touchpointId, bindings, Actor);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsMeasured.Should().BeTrue();
        // Full replace: the repo receives the complete authoritative set, all stamped with the
        // parent touchpoint id (the implementation DELETEs + INSERTs atomically inside the tx).
        await _touchpoints.Received(1).ReplaceKpiBindingsAsync(
            touchpointId,
            Arg.Is<IReadOnlyList<KpiBinding>>(b =>
                b.Count == 2
                && b.All(x => x.TouchpointId == touchpointId)
                && b.Any(x => x.KpiType == "NPS" && x.Weight == 60.00m)
                && b.Any(x => x.KpiType == "CSAT" && x.Weight == 40.00m)),
            Arg.Any<CancellationToken>());
        await _events.Received(1).PublishAsync(
            Arg.Is<CustomerJourneyManagementEvent>(e => e.EventType == CustomerJourneyManagementEventTypes.JourneyKpiBindingsUpdated),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveKpiBindingsAsync_sets_npsWarning_true_when_NPS_is_in_the_binding_set()
    {
        var touchpointId = GivenTouchpointInJourney("Active");
        var bindings = new[] { new KpiBindingInput("NPS", 60.00m), new KpiBindingInput("CSAT", 40.00m) };

        var result = await CreateSut().SaveKpiBindingsAsync(touchpointId, bindings, Actor);

        result.IsSuccess.Should().BeTrue();
        result.Value!.NpsWarning.Should().BeTrue();
    }

    [Fact]
    public async Task SaveKpiBindingsAsync_sets_npsWarning_false_when_NPS_is_absent()
    {
        // Proves the flag is conditional on the set, not hard-coded true.
        var touchpointId = GivenTouchpointInJourney("Active");
        var bindings = new[] { new KpiBindingInput("CSAT", 50.00m), new KpiBindingInput("CES", 50.00m) };

        var result = await CreateSut().SaveKpiBindingsAsync(touchpointId, bindings, Actor);

        result.IsSuccess.Should().BeTrue();
        result.Value!.NpsWarning.Should().BeFalse();
    }

    [Fact]
    public async Task SaveKpiBindingsAsync_returns_archived_immutable_when_parent_journey_is_archived()
    {
        var touchpointId = GivenTouchpointInJourney("Archived");
        var bindings = new[] { new KpiBindingInput("NPS", 60.00m), new KpiBindingInput("CSAT", 40.00m) };

        var result = await CreateSut().SaveKpiBindingsAsync(touchpointId, bindings, Actor);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("journey.archived_immutable");
        // No write and no event on a rejected save.
        await _touchpoints.DidNotReceive().ReplaceKpiBindingsAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<KpiBinding>>(),
            Arg.Any<CancellationToken>());
        await _events.DidNotReceive().PublishAsync(
            Arg.Any<CustomerJourneyManagementEvent>(), Arg.Any<CancellationToken>());
    }
}
