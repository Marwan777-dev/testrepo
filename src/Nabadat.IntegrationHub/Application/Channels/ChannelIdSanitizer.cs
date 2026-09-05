namespace Nabadat.IntegrationHub.Application.Channels;

/// <summary>
/// T029 — enforces VR-F04 / BR-04's shape rule for a service channel ID: letters, digits and hyphens only,
/// capped at <see cref="MaxLength"/> characters, <b>case preserved</b>.
///
/// <para>SCR-04 applies the same rule live as the user types (AC-S4-01), but the client is not the
/// authority: this sanitiser also runs on the write path, so a caller that posts a raw
/// <c>"My kiosk #1"</c> gets the same <c>"Mykiosk1"</c> the console would have produced instead of a
/// database CHECK violation surfacing as a 500.</para>
///
/// <para>Order matters — <b>strip, then truncate</b>. Truncating first would let removed characters
/// (spaces, punctuation) consume the 19-character budget and silently drop real ones.</para>
/// </summary>
public sealed class ChannelIdSanitizer
{
    /// <summary>
    /// VR-F04's "under 20 characters" cap, mirrored by SCR-04's <c>maxlength</c> and by the baseline's
    /// <c>ck_service_channels_channel_id_length</c> CHECK.
    /// </summary>
    public const int MaxLength = 19;

    /// <summary>
    /// Returns <paramref name="raw"/> reduced to its <c>[A-Za-z0-9-]</c> characters and truncated to
    /// <see cref="MaxLength"/>. Never returns <c>null</c>: <c>null</c> input, or input with no valid
    /// character at all, yields <see cref="string.Empty"/>, which the caller's required-field check reads as
    /// a missing ID.
    /// </summary>
    public string Sanitize(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        var kept = new char[Math.Min(raw.Length, MaxLength)];
        var length = 0;

        foreach (var character in raw)
        {
            if (!IsAllowed(character))
            {
                continue;
            }

            kept[length++] = character;

            if (length == MaxLength)
            {
                break;
            }
        }

        return length == 0 ? string.Empty : new string(kept, 0, length);
    }

    private static bool IsAllowed(char character) =>
        character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-';
}
