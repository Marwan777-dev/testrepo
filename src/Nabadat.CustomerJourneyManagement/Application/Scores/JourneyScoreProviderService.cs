using System.Text.Json;
using Nabadat.Platform.Contracts.M16;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Application.Scores;

/// <summary>
/// M-16's published <see cref="IJourneyScoreProvider"/> implementation (T069 / US-3): consumers
/// (M-06's batch refresh, M-07) call this to (re)compute and read a journey's latest scores. It is a
/// thin orchestration over the documented execution contract
/// (<c>contracts/published-interfaces.md §IJourneyScoreProvider</c>):
/// <list type="number">
///   <item><description>
///     Read the journey configuration via <see cref="IJourneyConfigReader"/>. A journey with no
///     config (it does not exist, or has no measured touchpoints) yields <see langword="null"/> and
///     <b>no</b> computation, write, or event.
///   </description></item>
///   <item><description>
///     Delegate the actual scoring to M-06 through the in-module <see cref="IM06ScoringService"/>
///     consumer port. If M-06 throws, the error propagates and the transaction below is
///     <b>never started</b>, so no partial score row or audit event is written.
///   </description></item>
///   <item><description>
///     In one transaction (<see cref="ITenantDbContext.ExecuteAsync(Func{Task}, CancellationToken)"/>): upsert the computed composite + score
///     trees to <c>journey_scores</c> (one row per journey) and publish <c>journey.score.updated</c>
///     to M-17 — atomically (FR-015). The M-06 result is then returned to the caller verbatim.
///   </description></item>
/// </list>
/// <para>
/// <b>System actor.</b> A score refresh is system-triggered (M-06 batch / on-demand recompute), not
/// a user action, so the published method carries no <see cref="ActorContext"/>. The audit event is
/// therefore stamped with a system actor (<see cref="SystemActorId"/> / <see cref="SystemPersona"/>)
/// and a fresh per-refresh correlation id, keeping the <c>event_log</c> row attributable.
/// </para>
/// Registered as <c>Scoped</c> (T069) — consumers receive it via constructor injection and never
/// instantiate M-16 concrete types.
/// </summary>
public sealed class JourneyScoreProviderService : IJourneyScoreProvider
{
    /// <summary>Per-journey score trees are persisted as camelCase JSON (matches the <c>journey_scores</c> jsonb shape).</summary>
    private static readonly JsonSerializerOptions ScoreJson = new(JsonSerializerDefaults.Web);

    /// <summary>No human actor stands behind a system score refresh; <c>event_log.actor_id</c> is nullable and accepts the empty uuid.</summary>
    private static readonly Guid SystemActorId = Guid.Empty;

    /// <summary>System-actor persona tag for the score-refresh audit event (fits <c>actor_persona varchar(16)</c>).</summary>
    private const string SystemPersona = "system";

    private readonly IJourneyConfigReader _configReader;
    private readonly IM06ScoringService _m06;
    private readonly IJourneyScoreDataService _scores;
    private readonly ITenantDbContext _db;
    private readonly IM17EventPublisher _events;
    private readonly TimeProvider _time;

    public JourneyScoreProviderService(
        IJourneyConfigReader configReader,
        IM06ScoringService m06,
        IJourneyScoreDataService scores,
        ITenantDbContext db,
        IM17EventPublisher events,
        TimeProvider time)
    {
        _configReader = configReader;
        _m06 = m06;
        _scores = scores;
        _db = db;
        _events = events;
        _time = time;
    }

    /// <inheritdoc />
    public async Task<JourneyScoreResultDto?> GetScoresAsync(Guid journeyId, CancellationToken ct = default)
    {
        // Step 1 — no config means no measured journey to score; return null, write nothing.
        var config = await _configReader.GetJourneyConfigAsync(journeyId, ct);
        if (config is null)
        {
            return null;
        }

        // Step 2 — delegate computation to M-06. A throw here propagates before any transaction
        // opens, so the persist/publish below never runs (no partial state).
        var result = await _m06.ComputeJourneyScoreAsync(config, ct);

        // Step 3 — persist the refreshed score and emit the audit event atomically (FR-015).
        var computedAt = _time.GetUtcNow();
        var correlationId = Guid.NewGuid();
        await _db.ExecuteAsync(async () =>
        {
            var score = new JourneyScore
            {
                JourneyScoreId = Guid.NewGuid(),
                JourneyId = journeyId,
                ComputedAt = computedAt,
                CompositeScore = result.JourneyScore,
                StageScores = JsonSerializer.Serialize(result.StageScores, ScoreJson),
                TouchpointScores = JsonSerializer.Serialize(result.TouchpointScores, ScoreJson),
            };
            await _scores.UpsertAsync(score, ct);

            await _events.PublishAsync(
                CustomerJourneyManagementEvent.JourneyScoreUpdated(
                    SystemActorId,
                    SystemPersona,
                    journeyId,
                    computedAt,
                    correlationId,
                    newValue: new { journeyId, journeyScore = result.JourneyScore }),
                ct);
        }, ct);

        return result;
    }
}
