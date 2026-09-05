namespace Nabadat.IntegrationHub.Application.Parameters.Dtos;

/// <summary>
/// The outcome of a parameter create or patch: either the persisted <see cref="Parameter"/> projection, or the
/// accumulated validation failures. Returned rather than thrown (mirroring <c>ServiceChannelSaveResult</c>) so the
/// controller maps codes to statuses in one place and SCR-06 receives every inline error at once.
///
/// <para><see cref="References"/> carries BR-10's third outcome — a <b>successful-shaped but unapplied</b>
/// disable. When the client asks to disable a parameter that has references and has not yet confirmed Dialog D-6,
/// the endpoint answers <b>200</b> with the reference list, the parameter <b>unchanged</b>, and
/// <see cref="RequiresDisableConfirmation"/> set. That is the resolved wire shape for the choice
/// contracts/api-endpoints.md deliberately left open: the impact warning is <i>informational</i>, so it cannot be
/// a 4xx, and it must not apply the change before the user has seen the list.</para>
/// </summary>
public sealed record ParameterSaveResult
{
    private ParameterSaveResult()
    {
    }

    /// <summary>True when the operation completed — including the "confirmation required" case, which is not a failure.</summary>
    public bool Succeeded => Errors.Count == 0;

    /// <summary>True when the change was withheld pending the user's acknowledgement of Dialog D-6 (BR-10).</summary>
    public bool RequiresDisableConfirmation { get; private init; }

    /// <summary>The persisted (or, when confirmation is pending, the unchanged) parameter. <c>null</c> only on failure.</summary>
    public ParameterDto? Parameter { get; private init; }

    /// <summary>BR-10's reference list — non-empty only when a disable was requested on a referenced parameter.</summary>
    public IReadOnlyList<ParameterReference> References { get; private init; } =
        Array.Empty<ParameterReference>();

    public IReadOnlyList<ParameterValidationError> Errors { get; private init; } =
        Array.Empty<ParameterValidationError>();

    public static ParameterSaveResult Ok(ParameterDto parameter) => new() { Parameter = parameter };

    /// <summary>The change was applied, and these references were affected — reported so the console can inform the user.</summary>
    public static ParameterSaveResult Applied(ParameterDto parameter, IReadOnlyList<ParameterReference> references) =>
        new() { Parameter = parameter, References = references };

    /// <summary>The disable was withheld: the client must re-send with the confirmation flag (BR-10).</summary>
    public static ParameterSaveResult ConfirmationRequired(
        ParameterDto parameter,
        IReadOnlyList<ParameterReference> references) =>
        new() { Parameter = parameter, References = references, RequiresDisableConfirmation = true };

    public static ParameterSaveResult Failed(IReadOnlyList<ParameterValidationError> errors) =>
        new() { Errors = errors };

    public static ParameterSaveResult Failed(string code, string message, string? field = null) =>
        new() { Errors = new[] { new ParameterValidationError(code, message, field) } };
}
