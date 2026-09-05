using FluentAssertions;
using Nabadat.KpiManagement.Application.ScoringConfig;
using Nabadat.Platform.Contracts.M16;
using NSubstitute;
using Xunit;

namespace Nabadat.KpiManagement.UnitTests.ScoringConfig;

/// <summary>
/// Unit tests for <see cref="ScoringConfigUpdateService"/> (T097 / US-4). The service validates the
/// payload, then delegates to M-16's published <see cref="IScoringConfigStore"/> — which owns the
/// atomic persist + the single <c>journey.scoring_config.updated</c> event. The service is
/// <b>idempotent</b>: it reads current state first and, when the payload matches, returns
/// <c>Idempotent</c> WITHOUT calling <see cref="IScoringConfigStore.UpdateAsync"/> (so no row write and
/// no event). The store is mocked; "1 row + 1 event" is proven by asserting <c>UpdateAsync</c> is
/// called exactly once (the real event emission is covered end-to-end by the integration suite, T114).
/// </summary>
public sealed class ScoringConfigUpdateServiceTests
{
    private static readonly ScoringConfigActor Actor = new(
        UserId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Persona: "P-01",
        CorrelationId: Guid.Parse("66666666-6666-6666-6666-666666666666"));

    private readonly IScoringConfigStore _store = Substitute.For<IScoringConfigStore>();

    private ScoringConfigUpdateService CreateSut() => new(new ScoringConfigValidator(), _store);

    private static ScoringConfigDto Dto(decimal alpha, decimal mot, int nFloor, int flagP, int window) =>
        new(alpha, 1.000m - alpha, mot, nFloor, flagP, window, DateTimeOffset.UnixEpoch, Actor.UserId);

    private static ScoringConfigInput Input(decimal alpha = 0.700m, decimal mot = 1.5m, int nFloor = 100, int flagP = 25, int window = 30) =>
        new(alpha, mot, nFloor, flagP, window);

    [Fact]
    public async Task UpdateAsync_calls_store_update_once_when_payload_changes_a_field()
    {
        // Current α=0.500; the payload sets α=0.700 — a real change, so the store is written.
        _store.GetAsync(Arg.Any<CancellationToken>()).Returns(Dto(0.500m, 1.5m, 100, 25, 30));
        _store.UpdateAsync(Arg.Any<ScoringConfigUpdate>(), Arg.Any<ScoringConfigActor>(), Arg.Any<CancellationToken>())
            .Returns(ScoringConfigUpdateResult.Success(Dto(0.700m, 1.5m, 100, 25, 30)));

        var result = await CreateSut().UpdateAsync(Input(alpha: 0.700m), Actor);

        result.Status.Should().Be(ScoringConfigSaveStatus.Updated);
        result.Config!.Alpha.Should().Be(0.700m);
        result.Config.Beta.Should().Be(0.300m);
        await _store.Received(1).UpdateAsync(
            Arg.Is<ScoringConfigUpdate>(u => u.Alpha == 0.700m && u.MotMultiplier == 1.5m),
            Arg.Is<ScoringConfigActor>(a => a.UserId == Actor.UserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_is_idempotent_and_emits_nothing_when_payload_matches_current()
    {
        // Current state equals the incoming payload → no write, no event.
        _store.GetAsync(Arg.Any<CancellationToken>()).Returns(Dto(0.700m, 1.5m, 100, 25, 30));

        var result = await CreateSut().UpdateAsync(Input(alpha: 0.700m), Actor);

        result.Status.Should().Be(ScoringConfigSaveStatus.Idempotent);
        await _store.DidNotReceive().UpdateAsync(
            Arg.Any<ScoringConfigUpdate>(), Arg.Any<ScoringConfigActor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_returns_failed_and_skips_the_store_when_payload_is_invalid()
    {
        var result = await CreateSut().UpdateAsync(Input(alpha: 1.5m), Actor);

        result.Status.Should().Be(ScoringConfigSaveStatus.Failed);
        result.ErrorCode.Should().Be(ScoringConfigValidator.AlphaOutOfRangeCode);
        await _store.DidNotReceive().GetAsync(Arg.Any<CancellationToken>());
        await _store.DidNotReceive().UpdateAsync(
            Arg.Any<ScoringConfigUpdate>(), Arg.Any<ScoringConfigActor>(), Arg.Any<CancellationToken>());
    }
}
