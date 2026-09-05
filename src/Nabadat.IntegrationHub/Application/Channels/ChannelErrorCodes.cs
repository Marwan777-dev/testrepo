namespace Nabadat.IntegrationHub.Application.Channels;

/// <summary>
/// The stable error codes the service-channel write path emits (T029–T034), and which
/// <c>ServiceChannelsController</c> maps to HTTP statuses per contracts/api-endpoints.md. Codes — not
/// messages — are the contract: the console copy in <see cref="ChannelValidationError.Message"/> may be
/// reworded or localised without breaking a client, the code may not.
///
/// <para>Status mapping owned by the controller: <c>duplicate_*</c> and <see cref="ChannelIdLocked"/> →
/// <b>409</b>, <see cref="ChannelNotFound"/> → <b>404</b>, every other <c>validation.*</c> → <b>400</b>.</para>
/// </summary>
public static class ChannelErrorCodes
{
    /// <summary>VR-F02 — Channel name · EN is required.</summary>
    public const string NameEnRequired = "validation.name_en_required";

    /// <summary>VR-F02 — Channel name · EN exceeds <see cref="ChannelNameValidator.MaxNameLength"/>.</summary>
    public const string NameEnTooLong = "validation.name_en_too_long";

    /// <summary>VR-F03 — Channel name · AR is required.</summary>
    public const string NameArRequired = "validation.name_ar_required";

    /// <summary>Channel name · AR exceeds <see cref="ChannelNameValidator.MaxNameLength"/> (baseline CHECK).</summary>
    public const string NameArTooLong = "validation.name_ar_too_long";

    /// <summary>VR-F02 — another channel already uses this EN name (case-insensitively). → 409.</summary>
    public const string DuplicateName = "validation.duplicate_name";

    /// <summary>VR-F04 — the service channel ID is required (and cannot be all-invalid characters).</summary>
    public const string ChannelIdRequired = "validation.channel_id_required";

    /// <summary>VR-F04 — another channel already uses this ID (case-insensitively). → 409.</summary>
    public const string DuplicateChannelId = "validation.duplicate_channel_id";

    /// <summary>BR-05 — the ID is locked by the channel's first 2xx request and can no longer change. → 409.</summary>
    public const string ChannelIdLocked = "channel.id_locked";

    /// <summary>VR-F13 — the tenant is already at its NFR-16 ceiling of service channels.</summary>
    public const string CapacityExceeded = "validation.capacity_exceeded";

    /// <summary>A submitted contract row references a parameter that does not exist in the catalogue.</summary>
    public const string UnknownParameter = "validation.unknown_parameter";

    /// <summary>The addressed channel does not exist. → 404.</summary>
    public const string ChannelNotFound = "channel.not_found";
}
