namespace Nabadat.IntegrationHub.Application.Channels;

/// <summary>
/// Outcome of one service-channel validator (T029–T033). Failures <b>accumulate</b> so SCR-04 can render
/// every inline error in a single pass instead of one per save round-trip.
/// </summary>
/// <param name="IsValid">True when <paramref name="Errors"/> is empty.</param>
/// <param name="Errors">Every failure found, in the order the validator checked them.</param>
public sealed record ChannelValidationResult(bool IsValid, IReadOnlyList<ChannelValidationError> Errors)
{
    /// <summary>The shared "nothing wrong" result.</summary>
    public static ChannelValidationResult Valid { get; } = new(true, Array.Empty<ChannelValidationError>());

    /// <summary>Builds a failed result from one or more errors.</summary>
    public static ChannelValidationResult Invalid(params ChannelValidationError[] errors) =>
        new(false, errors);

    /// <summary>Builds a failed result, or <see cref="Valid"/> when <paramref name="errors"/> is empty.</summary>
    public static ChannelValidationResult From(IReadOnlyList<ChannelValidationError> errors) =>
        errors.Count == 0 ? Valid : new ChannelValidationResult(false, errors);

    /// <summary>True when any accumulated error carries the given <see cref="ChannelErrorCodes"/> constant.</summary>
    public bool HasCode(string code) => Errors.Any(e => e.Code == code);

    /// <summary>The accumulated console messages, for inline rendering and test assertions.</summary>
    public IReadOnlyList<string> Messages => Errors.Select(e => e.Message).ToList();
}
