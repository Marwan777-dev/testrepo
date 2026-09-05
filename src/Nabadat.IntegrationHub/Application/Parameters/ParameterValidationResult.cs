namespace Nabadat.IntegrationHub.Application.Parameters;

/// <summary>
/// Outcome of one parameter-catalogue validator (T051–T056). Failures <b>accumulate</b> so SCR-06's drawer can
/// render every inline error in a single pass instead of one per save round-trip.
/// </summary>
/// <param name="IsValid">True when <paramref name="Errors"/> is empty.</param>
/// <param name="Errors">Every failure found, in the order the validator checked them.</param>
public sealed record ParameterValidationResult(bool IsValid, IReadOnlyList<ParameterValidationError> Errors)
{
    /// <summary>The shared "nothing wrong" result.</summary>
    public static ParameterValidationResult Valid { get; } = new(true, Array.Empty<ParameterValidationError>());

    /// <summary>Builds a failed result from one or more errors.</summary>
    public static ParameterValidationResult Invalid(params ParameterValidationError[] errors) => new(false, errors);

    /// <summary>Builds a failed result, or <see cref="Valid"/> when <paramref name="errors"/> is empty.</summary>
    public static ParameterValidationResult From(IReadOnlyList<ParameterValidationError> errors) =>
        errors.Count == 0 ? Valid : new ParameterValidationResult(false, errors);

    /// <summary>True when any accumulated error carries the given <see cref="ParameterErrorCodes"/> constant.</summary>
    public bool HasCode(string code) => Errors.Any(e => e.Code == code);

    /// <summary>The accumulated console messages, for inline rendering and test assertions.</summary>
    public IReadOnlyList<string> Messages => Errors.Select(e => e.Message).ToList();
}
