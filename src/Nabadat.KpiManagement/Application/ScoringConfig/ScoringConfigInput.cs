namespace Nabadat.KpiManagement.Application.ScoringConfig;

/// <summary>
/// The five tenant-level Customer Journey scoring parameters a P-01 edits (US-4 / SRS §11.7). β is
/// derived (<c>1 − α</c>) and is never part of the input — see <see cref="AlphaBetaDeriver"/>.
/// </summary>
public sealed record ScoringConfigInput(
    decimal Alpha,
    decimal MotMultiplier,
    int NFloor,
    int FlagPercentile,
    int RollingWindowDays);
