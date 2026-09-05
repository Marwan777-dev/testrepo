namespace Nabadat.IntegrationHub.Application.Channels;

/// <summary>
/// The wire field names a <see cref="ChannelValidationError"/> can point at — the same snake_case keys the
/// SCR-04 request body uses, so the API-05 envelope's <c>details[].field</c> matches what the client sent and
/// the console can attach each error to the right input.
/// </summary>
public static class ChannelFields
{
    /// <summary>Channel name · EN.</summary>
    public const string NameEn = "name_en";

    /// <summary>Channel name · AR.</summary>
    public const string NameAr = "name_ar";

    /// <summary>The inbound path segment (VR-F04).</summary>
    public const string ChannelId = "channel_id";

    /// <summary>The parameter-contract row collection.</summary>
    public const string Contract = "contract";
}
