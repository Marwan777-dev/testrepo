namespace Nabadat.IntegrationHub.Application.Parameters;

/// <summary>
/// One parameter-catalogue validation failure: a stable <paramref name="Code"/> from
/// <see cref="ParameterErrorCodes"/> (what the API layer maps to a status), the shipped inline console
/// <paramref name="Message"/> SCR-06 renders under the offending field, and the wire <paramref name="Field"/>
/// that identifies which field it belongs under.
///
/// <para>Mirrors <c>ChannelValidationError</c> (US1) deliberately: the two sub-domains' write paths report
/// failures the same shape, so the API-05 envelope building is identical in both controllers.</para>
/// </summary>
/// <param name="Code">A <see cref="ParameterErrorCodes"/> constant.</param>
/// <param name="Message">Console copy, worded per spec.md's normative message patterns.</param>
/// <param name="Field">The wire field name (<c>api_field</c>, <c>range_min</c>, …), or <c>null</c> for a form-level failure.</param>
public sealed record ParameterValidationError(string Code, string Message, string? Field = null);
