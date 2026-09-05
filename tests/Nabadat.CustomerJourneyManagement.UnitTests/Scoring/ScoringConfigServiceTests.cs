using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Scoring;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using NSubstitute;
using Xunit;

namespace Nabadat.CustomerJourneyManagement.UnitTests.Scoring;

/// <summary>
/// Unit tests for <see cref="ScoringConfigService"/> (T044a / US-2 Amendment) — the <b>tenant-level</b>
/// strategic scoring configuration (SRS §4.2.9 / §11.7, Q11 RESOLVED: per-tenant, NOT per-journey).
/// The config is a singleton — exactly one <c>scoring_configs</c> row per tenant — carrying the five
/// strategic parameters <c>alpha</c> / <c>mot_multiplier</c> / <c>n_floor</c> / <c>flag_percentile</c>
/// / <c>rolling_window_days</c>. These tests pin the contract the implementation must satisfy:
/// <list type="bullet">
///   <item><c>record SaveScoringConfigInput(decimal Alpha, decimal MotMultiplier, int NFloor,
///   int FlagPercentile, int RollingWindowDays)</c> — no <c>journeyId</c>.</item>
///   <item><c>IScoringConfigDataService</c> (Domain port) — <c>Task&lt;ScoringConfig?&gt; GetAsync(CancellationToken)</c>
///   (the single row, or null) and <c>Task&lt;ScoringConfig&gt; UpsertAsync(ScoringConfig, CancellationToken)</c>
///   (singleton upsert returning the persisted row).</item>
///   <item><c>ScoringConfigService(IScoringConfigDataService, ITenantDbContext, IM17EventPublisher, TimeProvider)</c>.</item>
///   <item><c>Task&lt;ServiceResult&lt;ScoringConfig&gt;&gt; SaveScoringConfigAsync(SaveScoringConfigInput input,
///   ActorContext actor, CancellationToken ct = default)</c> — validates the five parameters, upserts the
///   singleton, and publishes <c>journey.scoring_config.updated</c> in the SAME transaction (FR-015).</item>
///   <item><c>Task&lt;ScoringConfig&gt; GetScoringConfigAsync(CancellationToken ct = default)</c> — returns the
///   persisted singleton, or the seeded defaults (α=0.500, MOT=1.5, n_floor=100, flag_percentile=25,
///   rolling_window_days=30) when no row exists yet.</item>
/// </list>
/// The fake <see cref="ITenantDbContext"/> runs the unit-of-work inline; the data service and event
/// publisher are NSubstitute mocks. The genuine atomic commit/rollback is proven by the integration suite (T054a).
/// </summary>
public sealed class ScoringConfigServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly ActorContext Actor = new(
        UserId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Persona: "P-01",
        CorrelationId: Guid.Parse("66666666-6666-6666-6666-666666666666"));

    private readonly IScoringConfigDataService _scoringConfigs = Substitute.For<IScoringConfigDataService>();
    private readonly IM17EventPublisher _events = Substitute.For<IM17EventPublisher>();
    private readonly FakeTimeProvider _time = new(Now);

    public ScoringConfigServiceTests()
    {
        // UpsertAsync returns the persisted row (canonical id/created_at preserved on the real impl).
        _scoringConfigs.UpsertAsync(Arg.Any<ScoringConfig>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<ScoringConfig>());
    }

    private ScoringConfigService CreateSut() => new(
        _scoringConfigs,
        TestSupport.FakeTenantDb.Immediate(),
        _events,
        _time);

    private static SaveScoringConfigInput ValidInput() => new(
        Alpha: 0.700m,
        MotMultiplier: 1.5m,
        NFloor: 100,
        FlagPercentile: 25,
        RollingWindowDays: 30);

    [Fact]
    public async Task SaveScoringConfigAsync_upserts_singleton_and_publishes_event_when_input_is_valid()
    {
        var result = await CreateSut().SaveScoringConfigAsync(ValidInput(), Actor);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Alpha.Should().Be(0.700m);
        result.Value.MotMultiplier.Should().Be(1.5m);
        result.Value.NFloor.Should().Be(100);
        result.Value.FlagPercentile.Should().Be(25);
        result.Value.RollingWindowDays.Should().Be(30);
        // UpdatedAt/UpdatedBy are stamped from the injected TimeProvider + actor (no DateTime.UtcNow).
        result.Value.UpdatedAt.Should().Be(Now);
        result.Value.UpdatedBy.Should().Be(Actor.UserId);

        // The singleton is upserted carrying the requested parameters...
        await _scoringConfigs.Received(1).UpsertAsync(
            Arg.Is<ScoringConfig>(c =>
                c.Alpha == 0.700m
                && c.MotMultiplier == 1.5m
                && c.NFloor == 100
                && c.FlagPercentile == 25
                && c.RollingWindowDays == 30),
            Arg.Any<CancellationToken>());
        // ...and the audit event is published in the same transaction (FR-015).
        await _events.Received(1).PublishAsync(
            Arg.Is<CustomerJourneyManagementEvent>(e =>
                e.EventType == CustomerJourneyManagementEventTypes.JourneyScoringConfigUpdated
                && e.ActorId == Actor.UserId
                && e.CorrelationId == Actor.CorrelationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetScoringConfigAsync_returns_seeded_defaults_when_no_row_exists()
    {
        _scoringConfigs.GetAsync(Arg.Any<CancellationToken>()).Returns((ScoringConfig?)null);

        var result = await CreateSut().GetScoringConfigAsync();

        result.Should().NotBeNull();
        result.Alpha.Should().Be(0.500m);
        result.MotMultiplier.Should().Be(1.5m);
        result.NFloor.Should().Be(100);
        result.FlagPercentile.Should().Be(25);
        result.RollingWindowDays.Should().Be(30);
    }

    [Fact]
    public async Task GetScoringConfigAsync_returns_the_saved_row_when_one_exists()
    {
        var saved = new ScoringConfig
        {
            ScoringConfigId = Guid.NewGuid(),
            Alpha = 0.300m,
            MotMultiplier = 2.0m,
            NFloor = 50,
            FlagPercentile = 10,
            RollingWindowDays = 14,
            CreatedAt = Now,
            UpdatedAt = Now,
            UpdatedBy = Actor.UserId,
        };
        _scoringConfigs.GetAsync(Arg.Any<CancellationToken>()).Returns(saved);

        var result = await CreateSut().GetScoringConfigAsync();

        result.Should().BeSameAs(saved);
    }

    [Theory]
    [InlineData(-0.001, "scoring.alpha_out_of_range")]
    [InlineData(1.001, "scoring.alpha_out_of_range")]
    public async Task SaveScoringConfigAsync_rejects_alpha_out_of_range(double alpha, string code)
    {
        var input = ValidInput() with { Alpha = (decimal)alpha };

        var result = await CreateSut().SaveScoringConfigAsync(input, Actor);

        await AssertRejectedAsync(result, code);
    }

    [Theory]
    [InlineData(0.9, "scoring.mot_multiplier_out_of_range")]
    [InlineData(2.1, "scoring.mot_multiplier_out_of_range")]
    public async Task SaveScoringConfigAsync_rejects_mot_multiplier_out_of_range(double mot, string code)
    {
        var input = ValidInput() with { MotMultiplier = (decimal)mot };

        var result = await CreateSut().SaveScoringConfigAsync(input, Actor);

        await AssertRejectedAsync(result, code);
    }

    [Fact]
    public async Task SaveScoringConfigAsync_rejects_n_floor_below_minimum()
    {
        var result = await CreateSut().SaveScoringConfigAsync(ValidInput() with { NFloor = 0 }, Actor);

        await AssertRejectedAsync(result, "scoring.n_floor_below_minimum");
    }

    [Theory]
    [InlineData(0, "scoring.flag_percentile_out_of_range")]
    [InlineData(50, "scoring.flag_percentile_out_of_range")]
    public async Task SaveScoringConfigAsync_rejects_flag_percentile_out_of_range(int percentile, string code)
    {
        var result = await CreateSut().SaveScoringConfigAsync(ValidInput() with { FlagPercentile = percentile }, Actor);

        await AssertRejectedAsync(result, code);
    }

    [Fact]
    public async Task SaveScoringConfigAsync_rejects_rolling_window_below_minimum()
    {
        var result = await CreateSut().SaveScoringConfigAsync(ValidInput() with { RollingWindowDays = 6 }, Actor);

        await AssertRejectedAsync(result, "scoring.rolling_window_below_minimum");
    }

    /// <summary>A rejected save returns the error code and writes nothing — no upsert, no event.</summary>
    private async Task AssertRejectedAsync(ServiceResult<ScoringConfig> result, string expectedCode)
    {
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(expectedCode);
        await _scoringConfigs.DidNotReceive().UpsertAsync(Arg.Any<ScoringConfig>(), Arg.Any<CancellationToken>());
        await _events.DidNotReceive().PublishAsync(Arg.Any<CustomerJourneyManagementEvent>(), Arg.Any<CancellationToken>());
    }
}
