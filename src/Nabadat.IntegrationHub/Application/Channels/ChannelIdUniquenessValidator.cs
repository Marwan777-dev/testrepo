namespace Nabadat.IntegrationHub.Application.Channels;

/// <summary>
/// T030 — enforces VR-F04's uniqueness half: a service channel ID is unique per tenant
/// <b>case-insensitively</b> (matching VR-F01's convention for integration names), so <c>KIOSK-01</c> and
/// <c>kiosk-01</c> collide. The database backs this with the functional
/// <c>service_channels_channel_id_lower_uniq</c> index; this validator turns the same rule into the inline
/// console error before the write is attempted.
///
/// <para>Pure by design: the caller supplies the tenant's existing IDs — already excluding the row being
/// edited — so one instance serves both the create and update paths and needs no database access.</para>
///
/// <para>Shape/format is <see cref="ChannelIdSanitizer"/>'s job, not this type's. What arrives here is
/// already sanitised, which is why an empty value means "the user typed nothing usable".</para>
/// </summary>
public sealed class ChannelIdUniquenessValidator
{
    /// <summary>
    /// Checks that <paramref name="channelId"/> is present and unused. <paramref name="existingIds"/> may be
    /// <c>null</c> or empty — that simply means there is nothing to collide with.
    /// </summary>
    public ChannelValidationResult Validate(IEnumerable<string>? existingIds, string? channelId)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return ChannelValidationResult.Invalid(new ChannelValidationError(
                ChannelErrorCodes.ChannelIdRequired,
                "Service channel ID is required",
                ChannelFields.ChannelId));
        }

        var collides = existingIds is not null
            && existingIds.Any(existing => string.Equals(existing, channelId, StringComparison.OrdinalIgnoreCase));

        return collides
            ? ChannelValidationResult.Invalid(new ChannelValidationError(
                ChannelErrorCodes.DuplicateChannelId,
                "A channel with this ID already exists",
                ChannelFields.ChannelId))
            : ChannelValidationResult.Valid;
    }
}
