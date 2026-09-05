using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using Nabadat.Platform.Contracts.M16;

namespace Nabadat.CustomerJourneyManagement.Application.Scoring;

/// <summary>
/// The <b>tenant-level</b> strategic scoring-configuration service (T048a / US-2 Amendment, SRS
/// §4.2.9 / §11.7, Q11 RESOLVED: per-tenant, NOT per-journey). A tenant has exactly one
/// <c>scoring_configs</c> row (singleton) holding the five strategic parameters
/// <c>alpha</c> / <c>mot_multiplier</c> / <c>n_floor</c> / <c>flag_percentile</c> /
/// <c>rolling_window_days</c>; the save is an upsert.
/// <list type="bullet">
///   <item><description>
///     <b>M-16 validates and owns these parameters</b> (unlike the old per-journey model, where the
///     algorithm/normalization were M-06-owned pass-through). The five fields are range-checked here
///     (and backed by DB CHECK constraints); β is derived as <c>1 − α</c>, never stored.
///   </description></item>
///   <item><description>
///     <b>Persist + audit are one transaction</b> (FR-015): the upsert and the
///     <c>journey.scoring_config.updated</c> M-17 event commit together via
///     <see cref="ITenantDbContext.ExecuteAsync(Func{Task}, CancellationToken)"/>.
///   </description></item>
///   <item><description>
///     This service also implements the published <see cref="IScoringConfigStore"/> — the in-process
///     port M-06 reads scoring from, and feature 003's Settings → Customer Journey page writes through.
///   </description></item>
/// </list>
/// </summary>
public sealed class ScoringConfigService : IScoringConfigStore
{
    // Seeded defaults for a fresh tenant (mirror the scoring_configs column DEFAULTs / SRS §11.7).
    private const decimal DefaultAlpha = 0.500m;
    private const decimal DefaultMotMultiplier = 1.5m;
    private const int DefaultNFloor = 100;
    private const int DefaultFlagPercentile = 25;
    private const int DefaultRollingWindowDays = 30;

    private readonly IScoringConfigDataService _scoringConfigs;
    private readonly ITenantDbContext _db;
    private readonly IM17EventPublisher _events;
    private readonly TimeProvider _time;

    public ScoringConfigService(
        IScoringConfigDataService scoringConfigs,
        ITenantDbContext db,
        IM17EventPublisher events,
        TimeProvider time)
    {
        _scoringConfigs = scoringConfigs;
        _db = db;
        _events = events;
        _time = time;
    }

    // ── In-module API (unit-tested; T044a) ──────────────────────────────────────────

    /// <summary>
    /// Returns the tenant's saved scoring parameters, or the seeded defaults (α=0.500, MOT=1.5,
    /// n_floor=100, flag_percentile=25, rolling_window_days=30) when no row exists yet.
    /// </summary>
    public async Task<ScoringConfig> GetScoringConfigAsync(CancellationToken ct = default)
        => await _scoringConfigs.GetAsync(ct) ?? Defaults();

    /// <summary>
    /// Validates the five parameters, upserts the tenant's single scoring-config row, and publishes
    /// <c>journey.scoring_config.updated</c> in the same transaction (FR-015). On a validation failure
    /// nothing is written and the failing code is returned.
    /// </summary>
    public async Task<ServiceResult<ScoringConfig>> SaveScoringConfigAsync(
        SaveScoringConfigInput input,
        ActorContext actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var failure = Validate(input);
        if (failure is { } f)
        {
            return ServiceResult<ScoringConfig>.Failure(f.Code, f.Message);
        }

        var now = _time.GetUtcNow();
        var config = new ScoringConfig
        {
            ScoringConfigId = Guid.NewGuid(),
            Alpha = input.Alpha,
            MotMultiplier = input.MotMultiplier,
            NFloor = input.NFloor,
            FlagPercentile = input.FlagPercentile,
            RollingWindowDays = input.RollingWindowDays,
            CreatedAt = now,
            UpdatedAt = now,
            UpdatedBy = actor.UserId,
        };

        ScoringConfig persisted = config;
        await _db.ExecuteAsync(async () =>
        {
            // Singleton upsert + the audit event commit atomically (FR-015). UpsertAsync returns the
            // persisted row (canonical scoring_config_id / created_at survive an in-place replace).
            persisted = await _scoringConfigs.UpsertAsync(config, ct);

            await _events.PublishAsync(
                CustomerJourneyManagementEvent.JourneyScoringConfigUpdated(
                    actor.UserId,
                    actor.Persona,
                    persisted.ScoringConfigId,
                    now,
                    actor.CorrelationId,
                    newValue: new
                    {
                        persisted.Alpha,
                        persisted.MotMultiplier,
                        persisted.NFloor,
                        persisted.FlagPercentile,
                        persisted.RollingWindowDays,
                    }),
                ct);
        }, ct);

        return ServiceResult<ScoringConfig>.Success(persisted);
    }

    // ── Published IScoringConfigStore (cross-module: M-06 reads, feature 003 writes) ─────

    /// <inheritdoc />
    public async Task<ScoringConfigDto> GetAsync(CancellationToken ct = default)
        => ToDto(await GetScoringConfigAsync(ct));

    /// <inheritdoc />
    public async Task<ScoringConfigUpdateResult> UpdateAsync(
        ScoringConfigUpdate update,
        ScoringConfigActor actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(actor);

        var input = new SaveScoringConfigInput(
            update.Alpha,
            update.MotMultiplier,
            update.NFloor,
            update.FlagPercentile,
            update.RollingWindowDays);

        var result = await SaveScoringConfigAsync(
            input,
            new ActorContext(actor.UserId, actor.Persona, actor.CorrelationId),
            ct);

        return result.IsSuccess
            ? ScoringConfigUpdateResult.Success(ToDto(result.Value!))
            : ScoringConfigUpdateResult.Failure(result.Error!.Code, result.Error.Message);
    }

    private static ScoringConfig Defaults() => new()
    {
        ScoringConfigId = Guid.Empty,
        Alpha = DefaultAlpha,
        MotMultiplier = DefaultMotMultiplier,
        NFloor = DefaultNFloor,
        FlagPercentile = DefaultFlagPercentile,
        RollingWindowDays = DefaultRollingWindowDays,
    };

    /// <summary>β is derived (<c>1 − α</c>), never stored.</summary>
    private static ScoringConfigDto ToDto(ScoringConfig c) => new(
        c.Alpha,
        1.000m - c.Alpha,
        c.MotMultiplier,
        c.NFloor,
        c.FlagPercentile,
        c.RollingWindowDays,
        c.UpdatedAt,
        c.UpdatedBy);

    /// <summary>Range-checks the five parameters per SRS §11.7; returns the first failure or null.</summary>
    private static (string Code, string Message)? Validate(SaveScoringConfigInput i)
    {
        if (i.Alpha < 0m || i.Alpha > 1m)
        {
            return ("scoring.alpha_out_of_range", "Alpha must be between 0.000 and 1.000.");
        }

        if (i.MotMultiplier < 1.0m || i.MotMultiplier > 2.0m)
        {
            return ("scoring.mot_multiplier_out_of_range", "MOT multiplier must be between 1.0 and 2.0.");
        }

        if (i.NFloor < 1)
        {
            return ("scoring.n_floor_below_minimum", "Responses count floor (n_floor) must be at least 1.");
        }

        if (i.FlagPercentile < 1 || i.FlagPercentile > 49)
        {
            return ("scoring.flag_percentile_out_of_range", "Flag percentile must be between 1 and 49.");
        }

        if (i.RollingWindowDays < 7)
        {
            return ("scoring.rolling_window_below_minimum", "Rolling window must be at least 7 days.");
        }

        return null;
    }
}

/// <summary>
/// The five tenant-level scoring parameters to persist (SRS §4.2.9 / §11.7). β is derived as
/// <c>1 − α</c> and never supplied.
/// </summary>
public sealed record SaveScoringConfigInput(
    decimal Alpha,
    decimal MotMultiplier,
    int NFloor,
    int FlagPercentile,
    int RollingWindowDays);
