namespace Nabadat.IntegrationHub.Application.Channels;

/// <summary>
/// One service-channel validation failure: a stable <paramref name="Code"/> from
/// <see cref="ChannelErrorCodes"/> (what the API layer maps to a status), the shipped inline console
/// <paramref name="Message"/> SCR-04 renders under the offending field, and the wire
/// <paramref name="Field"/> that identifies which field it belongs under.
///
/// <para><paramref name="Field"/> lives here rather than in a controller-side code→field map so the two can
/// never drift: a validator that adds a code adds its field in the same line. It flows straight into the
/// API-05 envelope's <c>details[].field</c>, which is how the console attaches each error inline instead of
/// showing one form-level banner.</para>
/// </summary>
/// <param name="Code">A <see cref="ChannelErrorCodes"/> constant.</param>
/// <param name="Message">Console copy, worded per spec.md's normative message patterns.</param>
/// <param name="Field">The wire field name (<c>name_en</c>, <c>channel_id</c>, …), or <c>null</c> for a form-level failure.</param>
public sealed record ChannelValidationError(string Code, string Message, string? Field = null);
