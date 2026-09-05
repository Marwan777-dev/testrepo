using FluentValidation;

namespace Nabadat.KpiManagement.Application.ScoringConfig;

/// <summary>
/// Validates the five tenant scoring parameters (US-4 / SRS §11.7). Error codes are the domain codes
/// the controller maps to the API-05 wire codes (<c>INVALID_ALPHA_BETA_SUM</c>,
/// <c>MOT_MULTIPLIER_OUT_OF_RANGE</c>, …). These checks mirror the M-16 <c>scoring_configs</c> CHECK
/// constraints (defence in depth) and the M-16 store's own validation.
/// </summary>
public sealed class ScoringConfigValidator : AbstractValidator<ScoringConfigInput>
{
    public const string AlphaOutOfRangeCode = "alpha.out_of_range";
    public const string MotOutOfRangeCode = "mot_multiplier.out_of_range";
    public const string NFloorBelowMinimumCode = "n_floor.below_minimum";
    public const string FlagPercentileOutOfRangeCode = "flag_percentile.out_of_range";
    public const string RollingWindowBelowMinimumCode = "rolling_window.below_minimum";

    public ScoringConfigValidator()
    {
        RuleFor(x => x.Alpha)
            .InclusiveBetween(0.000m, 1.000m)
            .WithErrorCode(AlphaOutOfRangeCode)
            .WithMessage("Alpha must be between 0.000 and 1.000.");

        RuleFor(x => x.MotMultiplier)
            .InclusiveBetween(1.0m, 2.0m)
            .WithErrorCode(MotOutOfRangeCode)
            .WithMessage("MOT multiplier must be between 1.0 and 2.0.");

        RuleFor(x => x.NFloor)
            .GreaterThanOrEqualTo(1)
            .WithErrorCode(NFloorBelowMinimumCode)
            .WithMessage("Responses count floor must be at least 1.");

        RuleFor(x => x.FlagPercentile)
            .InclusiveBetween(1, 49)
            .WithErrorCode(FlagPercentileOutOfRangeCode)
            .WithMessage("Flag percentile must be between 1 and 49.");

        RuleFor(x => x.RollingWindowDays)
            .GreaterThanOrEqualTo(7)
            .WithErrorCode(RollingWindowBelowMinimumCode)
            .WithMessage("Rolling window must be at least 7 days.");
    }
}
