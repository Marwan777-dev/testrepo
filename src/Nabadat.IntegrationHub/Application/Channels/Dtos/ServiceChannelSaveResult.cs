namespace Nabadat.IntegrationHub.Application.Channels.Dtos;

/// <summary>
/// Outcome of a service-channel create or update. Validation failures are <b>returned, not thrown</b>: they
/// are expected outcomes of user input, and the controller needs the codes to pick the status
/// (<c>duplicate_*</c> / <c>channel.id_locked</c> → 409, <c>channel.not_found</c> → 404, other
/// <c>validation.*</c> → 400) and the messages for the API-05 envelope's <c>details</c>.
/// </summary>
/// <param name="Succeeded">True when the write committed.</param>
/// <param name="Channel">The persisted projection on success; <c>null</c> on failure.</param>
/// <param name="Errors">Every failure found; empty on success.</param>
public sealed record ServiceChannelSaveResult(
    bool Succeeded,
    ServiceChannelDto? Channel,
    IReadOnlyList<ChannelValidationError> Errors)
{
    /// <summary>A committed write.</summary>
    public static ServiceChannelSaveResult Ok(ServiceChannelDto channel) =>
        new(true, channel, Array.Empty<ChannelValidationError>());

    /// <summary>A rejected write carrying every accumulated failure.</summary>
    public static ServiceChannelSaveResult Failed(IReadOnlyList<ChannelValidationError> errors) =>
        new(false, null, errors);

    /// <summary>A rejected write carrying a single failure.</summary>
    public static ServiceChannelSaveResult Failed(string code, string message, string? field = null) =>
        new(false, null, new[] { new ChannelValidationError(code, message, field) });
}
