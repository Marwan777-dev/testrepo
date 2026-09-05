using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.Platform.Contracts.M16;
using Nabadat.CustomerJourneyManagement.Application.Reports;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using NSubstitute;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.UnitTests.Reports;

/// <summary>
/// Unit tests for <see cref="ReportContractService"/> (T087 / US-4) — the M-07 report-contract
/// builder/rebuilder (research.md §8, <c>contracts/journeys-api.md §GET /reports</c>). Authored FIRST
/// (red→green per the Unit Test Policy); they define the contract the T087 implementation must satisfy,
/// expanding the Phase-2 no-op stub (T014b) into:
/// <list type="bullet">
///   <item><c>ReportContractService(IJourneyConfigReader, IReportContractDataService,
///   IDetectionDataService, TimeProvider)</c> — loads the journey tree (stages → touchpoints →
///   KPI bindings, including unmeasured touchpoints per the <c>IJourneyConfigReader</c> contract rule 3)
///   through the published reader rather than re-querying tables.</item>
///   <item><c>Task&lt;ReportContractDto?&gt; BuildContractAsync(Guid journeyId, CancellationToken ct =
///   default)</c> — projects the live tree into a <see cref="ReportContractDto"/>; returns <c>null</c>
///   when the journey config does not exist.</item>
///   <item><c>Task RebuildContractAsync(Guid journeyId, CancellationToken ct = default)</c> — builds the
///   DTO, serializes it to the <c>jsonb</c> payload, and UPSERTs <c>report_contracts.contract_payload</c>
///   inside the caller's <c>ITenantDbContext.ExecuteAsync</c> (FR-015). This replaces the stub's no-op
///   body; the call sites (<c>KpiBindingService</c>, <c>DetectionConfigService</c>) invoke it on their
///   own transaction.</item>
/// </list>
/// The build is a pure projection over the mocked <see cref="IJourneyConfigReader"/>: every touchpoint is
/// carried (FR — the contract enumerates all stages/touchpoints), an unmeasured touchpoint surfaces with
/// <c>IsMeasured = false</c> and an empty <c>KpiTypes</c> list (excluded from the KPI dimension list,
/// FR-008), and <c>ScoreDimensions</c> is the fixed Phase-1 quad. The payload assertion parses the
/// upserted JSON back (case-insensitively, so it is naming-policy-agnostic) to prove a real serialized
/// contract was written. The genuine same-transaction commit is proven by the integration suite
/// (T091/T092).
/// </summary>
public sealed class ReportContractServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly IJourneyConfigReader _journeyConfig = Substitute.For<IJourneyConfigReader>();
    private readonly IReportContractDataService _reportContracts = Substitute.For<IReportContractDataService>();
    private readonly IDetectionDataService _detection = Substitute.For<IDetectionDataService>();
    private readonly FakeTimeProvider _time = new(Now);

    private ReportContractService CreateSut() => new(
        _journeyConfig,
        _reportContracts,
        _detection,
        _time);

    /// <summary>
    /// Wires a journey "Onboarding" with one stage "Awareness" holding two touchpoints — a measured one
    /// (NPS 60 / CSAT 40) and an unmeasured one (no KPI bindings) — plus a 40/75 detection config.
    /// </summary>
    private (Guid journeyId, Guid stageId, Guid measuredTouchpointId, Guid unmeasuredTouchpointId) GivenJourneyConfig()
    {
        var journeyId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var measuredTouchpointId = Guid.NewGuid();
        var unmeasuredTouchpointId = Guid.NewGuid();

        var config = new JourneyConfigDto(
            journeyId,
            "Onboarding",
            JourneyConfigStatus.Active,
            new List<StageConfigDto>
            {
                new(stageId, SequenceNumber: 1, "Awareness", new List<TouchpointConfigDto>
                {
                    new(measuredTouchpointId, "Checkout", IsMoT: true, IsMandatory: true, IsMeasured: true,
                        new List<KpiBindingConfigDto>
                        {
                            new("NPS", 60m, IsPlatformStandard: true, ScoringDirection.Ascending),
                            new("CSAT", 40m, IsPlatformStandard: true, ScoringDirection.Ascending),
                        }),
                    new(unmeasuredTouchpointId, "Browse", IsMoT: false, IsMandatory: false, IsMeasured: false,
                        new List<KpiBindingConfigDto>()),
                }),
            });

        _journeyConfig.GetJourneyConfigAsync(journeyId, Arg.Any<CancellationToken>()).Returns(config);
        _detection.GetByJourneyAsync(journeyId, Arg.Any<CancellationToken>())
            .Returns(new DetectionConfig
            {
                DetectionConfigId = Guid.NewGuid(),
                JourneyId = journeyId,
                PainThreshold = 40m,
                HappyThreshold = 75m,
            });

        return (journeyId, stageId, measuredTouchpointId, unmeasuredTouchpointId);
    }

    [Fact]
    public async Task BuildContractAsync_returns_contract_with_all_stages_and_touchpoints()
    {
        var (journeyId, stageId, measuredTouchpointId, unmeasuredTouchpointId) = GivenJourneyConfig();

        var result = await CreateSut().BuildContractAsync(journeyId);

        result.Should().NotBeNull();
        result!.JourneyId.Should().Be(journeyId);
        result.JourneyName.Should().Be("Onboarding");
        result.Stages.Should().ContainSingle();
        var stage = result.Stages[0];
        stage.StageId.Should().Be(stageId);
        stage.Name.Should().Be("Awareness");
        stage.SequenceNumber.Should().Be(1);
        // Every touchpoint is enumerated — measured and unmeasured alike.
        stage.Touchpoints.Select(t => t.TouchpointId)
            .Should().BeEquivalentTo(new[] { measuredTouchpointId, unmeasuredTouchpointId });
        // ScoreDimensions is the fixed Phase-1 quad (published-interfaces contract rule 3).
        result.ScoreDimensions.Should().Equal("journey_score", "stage_score", "touchpoint_score", "kpi_score");
        result.DetectionConfig.PainThreshold.Should().Be(40m);
        result.DetectionConfig.HappyThreshold.Should().Be(75m);
    }

    [Fact]
    public async Task BuildContractAsync_marks_unmeasured_touchpoint_unmeasured_with_no_kpi_types()
    {
        var (journeyId, _, measuredTouchpointId, unmeasuredTouchpointId) = GivenJourneyConfig();

        var result = await CreateSut().BuildContractAsync(journeyId);

        var touchpoints = result!.Stages[0].Touchpoints;
        var measured = touchpoints.Single(t => t.TouchpointId == measuredTouchpointId);
        var unmeasured = touchpoints.Single(t => t.TouchpointId == unmeasuredTouchpointId);

        measured.IsMeasured.Should().BeTrue();
        measured.KpiTypes.Should().BeEquivalentTo(new[] { "NPS", "CSAT" });

        // Unmeasured: flagged false and contributes no KPI types (absent from the KPI dimension list).
        unmeasured.IsMeasured.Should().BeFalse();
        unmeasured.KpiTypes.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildContractAsync_returns_null_when_journey_config_does_not_exist()
    {
        // Companion to the happy path — proves Build reads through the config reader rather than being
        // hard-coded to a non-null contract.
        var journeyId = Guid.NewGuid();
        _journeyConfig.GetJourneyConfigAsync(journeyId, Arg.Any<CancellationToken>())
            .Returns((JourneyConfigDto?)null);

        var result = await CreateSut().BuildContractAsync(journeyId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RebuildContractAsync_serializes_the_contract_and_upserts_the_jsonb_payload()
    {
        var (journeyId, _, _, _) = GivenJourneyConfig();
        ReportContract? upserted = null;
        _reportContracts
            .When(r => r.UpsertAsync(Arg.Any<ReportContract>(), Arg.Any<CancellationToken>()))
            .Do(ci => upserted = ci.Arg<ReportContract>());

        await CreateSut().RebuildContractAsync(journeyId);

        // The rebuilt contract is UPSERTed for this journey, stamped from the injected TimeProvider,
        // with a non-empty serialized payload — all on the caller's transaction (FR-015).
        await _reportContracts.Received(1).UpsertAsync(
            Arg.Is<ReportContract>(c =>
                c.JourneyId == journeyId
                && c.GeneratedAt == Now
                && !string.IsNullOrWhiteSpace(c.ContractPayload)),
            Arg.Any<CancellationToken>());

        // The payload is the serialized contract — parse it back and spot-check the structure.
        upserted.Should().NotBeNull();
        using var doc = JsonDocument.Parse(upserted!.ContractPayload);
        var root = doc.RootElement;
        Prop(root, "journeyName").GetString().Should().Be("Onboarding");
        Prop(root, "stages").GetArrayLength().Should().Be(1);
    }

    /// <summary>Case-insensitive property lookup so the assertions don't pin a JSON naming policy.</summary>
    private static JsonElement Prop(JsonElement obj, string name)
    {
        foreach (var property in obj.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        throw new InvalidOperationException($"Expected property '{name}' in payload {obj.GetRawText()}.");
    }
}
