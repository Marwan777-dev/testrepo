using Nabadat.Platform.Contracts.M16;

namespace Nabadat.KpiManagement.Application.ScoringConfig;

/// <summary>
/// Orchestrates the tenant ScoringConfig editing surface (US-4 / FR-053–FR-061). Validates the five
/// parameters, then delegates the persist to M-16's published <see cref="IScoringConfigStore"/> — the
/// `scoring_configs` singleton, its `journey.scoring_config.updated` event, and the atomic transaction
/// all live on M-16's side (AD-01: M-06 never touches the M-16 table directly).
/// <para>
/// <b>Idempotent.</b> The service reads current state first and, when the payload matches every field,
/// returns <see cref="ScoringConfigSaveStatus.Idempotent"/> WITHOUT calling
/// <see cref="IScoringConfigStore.UpdateAsync"/> — so a no-op save writes nothing and emits no event
/// (spec Edge Cases "ScoringConfig idempotent save").
/// </para>
/// </summary>
public sealed class ScoringConfigUpdateService
{
    private readonly ScoringConfigValidator _validator;
    private readonly IScoringConfigStore _store;

    public ScoringConfigUpdateService(ScoringConfigValidator validator, IScoringConfigStore store)
    {
        _validator = validator;
        _store = store;
    }

    /// <summary>Reads the tenant's current scoring parameters (defaults on a fresh tenant), β derived.</summary>
    public Task<ScoringConfigDto> GetAsync(CancellationToken ct = default) => _store.GetAsync(ct);

    /// <summary>
    /// Validates <paramref name="input"/>, then (only when a field actually changes) delegates to
    /// <see cref="IScoringConfigStore.UpdateAsync"/>. Returns <see cref="ScoringConfigSaveStatus.Failed"/>
    /// with the validator's code on invalid input (the store is not touched).
    /// </summary>
    public async Task<ScoringConfigSaveResult> UpdateAsync(
        ScoringConfigInput input,
        ScoringConfigActor actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(actor);

        var validation = _validator.Validate(input);
        if (!validation.IsValid)
        {
            var error = validation.Errors[0];
            return ScoringConfigSaveResult.Failed(error.ErrorCode, error.ErrorMessage);
        }

        var current = await _store.GetAsync(ct);
        if (Matches(current, input))
        {
            return ScoringConfigSaveResult.Idempotent(current);
        }

        var result = await _store.UpdateAsync(
            new ScoringConfigUpdate(input.Alpha, input.MotMultiplier, input.NFloor, input.FlagPercentile, input.RollingWindowDays),
            actor,
            ct);

        return result.IsSuccess
            ? ScoringConfigSaveResult.Updated(result.Config!)
            : ScoringConfigSaveResult.Failed(result.ErrorCode!, result.ErrorMessage ?? "Scoring config update failed.");
    }

    private static bool Matches(ScoringConfigDto current, ScoringConfigInput input) =>
        current.Alpha == input.Alpha
        && current.MotMultiplier == input.MotMultiplier
        && current.NFloor == input.NFloor
        && current.FlagPercentile == input.FlagPercentile
        && current.RollingWindowDays == input.RollingWindowDays;
}
