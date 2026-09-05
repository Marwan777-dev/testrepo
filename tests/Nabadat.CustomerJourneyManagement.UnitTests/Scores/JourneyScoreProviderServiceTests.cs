using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.Platform.Contracts.M16;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Scores;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.UnitTests.Scores;

/// <summary>
/// Unit tests for <see cref="JourneyScoreProviderService"/> (T069 / US-3) — M-16's implementation of
/// the published <see cref="IJourneyScoreProvider"/>. It delegates score computation to M-06, then
/// persists the result and publishes <c>journey.score.updated</c> atomically
/// (<c>contracts/published-interfaces.md §IJourneyScoreProvider</c>). Authored FIRST (red→green); they
/// pin the contract the T069 implementation must satisfy:
/// <list type="bullet">
///   <item>An M-16-owned consumer port for M-06 — <c>interface IM06ScoringService { Task&lt;
///   JourneyScoreResultDto&gt; ComputeJourneyScoreAsync(JourneyConfigDto config, CancellationToken
///   = default); }</c> — declared in-module exactly as <c>IM11TenantService</c> is (no shared
///   contracts project; M-16 declares the narrow upstream port it consumes).</item>
///   <item><c>IJourneyScoreDataService</c> (Domain port) — <c>Task UpsertAsync(JourneyScore,
///   CancellationToken)</c> (one row per journey: INSERT … ON CONFLICT).</item>
///   <item><c>JourneyScoreProviderService(IJourneyConfigReader, IM06ScoringService,
///   IJourneyScoreDataService, ITransactionRunner, IM17EventPublisher, TimeProvider)</c>.</item>
/// </list>
/// Execution order (per the published execution contract): read config → if null, return null →
/// call M-06 → BEGIN TX → upsert journey_scores + publish event → COMMIT. If M-06 throws, the
/// transaction is never started, so no partial state is written and the error propagates.
/// </summary>
public sealed class JourneyScoreProviderServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly IJourneyConfigReader _configReader = Substitute.For<IJourneyConfigReader>();
    private readonly IM06ScoringService _m06 = Substitute.For<IM06ScoringService>();
    private readonly IJourneyScoreDataService _scores = Substitute.For<IJourneyScoreDataService>();
    private readonly IM17EventPublisher _events = Substitute.For<IM17EventPublisher>();
    private readonly FakeTimeProvider _time = new(Now);

    private JourneyScoreProviderService CreateSut() => new(
        _configReader,
        _m06,
        _scores,
        TestSupport.FakeTenantDb.Immediate(),
        _events,
        _time);

    private static JourneyConfigDto ConfigFor(Guid journeyId) => new(
        journeyId,
        "Customer Onboarding",
        JourneyConfigStatus.Active,
        Stages: []);

    private static JourneyScoreResultDto ResultFor(Guid journeyId, decimal score) => new(
        journeyId,
        score,
        ComputedAt: new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc),
        StageScores: [],
        TouchpointScores: []);

    [Fact]
    public async Task GetScoresAsync_delegates_to_M06_then_upserts_score_and_publishes_event_in_one_transaction()
    {
        var journeyId = Guid.NewGuid();
        var config = ConfigFor(journeyId);
        _configReader.GetJourneyConfigAsync(journeyId, Arg.Any<CancellationToken>()).Returns(config);
        _m06.ComputeJourneyScoreAsync(Arg.Any<JourneyConfigDto>(), Arg.Any<CancellationToken>())
            .Returns(ResultFor(journeyId, 78.5m));

        var result = await CreateSut().GetScoresAsync(journeyId);

        result.Should().NotBeNull();
        result!.JourneyId.Should().Be(journeyId);
        result.JourneyScore.Should().Be(78.5m);
        // Delegation: M-06 is handed the journey config produced by the reader.
        await _m06.Received(1).ComputeJourneyScoreAsync(
            Arg.Is<JourneyConfigDto>(c => c.JourneyId == journeyId),
            Arg.Any<CancellationToken>());
        // The computed composite score is upserted to journey_scores...
        await _scores.Received(1).UpsertAsync(
            Arg.Is<JourneyScore>(s => s.JourneyId == journeyId && s.CompositeScore == 78.5m),
            Arg.Any<CancellationToken>());
        // ...and journey.score.updated is published in the same transaction (FR-015).
        await _events.Received(1).PublishAsync(
            Arg.Is<CustomerJourneyManagementEvent>(e => e.EventType == CustomerJourneyManagementEventTypes.JourneyScoreUpdated && e.EntityId == journeyId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetScoresAsync_returns_null_and_skips_computation_when_journey_has_no_config()
    {
        var journeyId = Guid.NewGuid();
        _configReader.GetJourneyConfigAsync(journeyId, Arg.Any<CancellationToken>())
            .Returns((JourneyConfigDto?)null);

        var result = await CreateSut().GetScoresAsync(journeyId);

        result.Should().BeNull();
        await _m06.DidNotReceive().ComputeJourneyScoreAsync(Arg.Any<JourneyConfigDto>(), Arg.Any<CancellationToken>());
        await _scores.DidNotReceive().UpsertAsync(Arg.Any<JourneyScore>(), Arg.Any<CancellationToken>());
        await _events.DidNotReceive().PublishAsync(Arg.Any<CustomerJourneyManagementEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetScoresAsync_persists_nothing_when_M06_computation_fails()
    {
        // Per the execution contract, if M-06 throws, the transaction in step 3 is never started, so
        // no partial state is written (no score row, no event) and the error propagates to the caller.
        var journeyId = Guid.NewGuid();
        _configReader.GetJourneyConfigAsync(journeyId, Arg.Any<CancellationToken>()).Returns(ConfigFor(journeyId));
        _m06.ComputeJourneyScoreAsync(Arg.Any<JourneyConfigDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("M-06 scoring engine unavailable"));

        var act = async () => await CreateSut().GetScoresAsync(journeyId);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _scores.DidNotReceive().UpsertAsync(Arg.Any<JourneyScore>(), Arg.Any<CancellationToken>());
        await _events.DidNotReceive().PublishAsync(Arg.Any<CustomerJourneyManagementEvent>(), Arg.Any<CancellationToken>());
    }
}
