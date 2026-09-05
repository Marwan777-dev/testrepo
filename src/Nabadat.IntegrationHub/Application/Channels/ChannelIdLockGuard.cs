using Nabadat.IntegrationHub.Domain.Entities;

namespace Nabadat.IntegrationHub.Application.Channels;

/// <summary>
/// T031 — enforces BR-05 / FR-S4-02: the channel ID is editable until the channel's first successful (2xx)
/// request, then locked permanently. A pre-lock edit legitimately changes the endpoint path (the old ID
/// then resolves <c>E-1001</c>); a post-lock edit is a <b>409 <c>channel.id_locked</c></b>.
///
/// <para>The lock has two independent sources, OR-ed together:</para>
/// <list type="number">
///   <item>the persisted one-way <see cref="ServiceChannel.ChannelIdLocked"/> flag, set by the first 2xx in
///   US4's request pipeline; and</item>
///   <item>a live "has this channel logged a 2xx?" probe the caller passes in — defence in depth for the
///   case where traffic exists but the flag was never written.</item>
/// </list>
///
/// <para>The guard is pure: <c>ServiceChannelService</c> (T034) resolves the probe from
/// <c>integration_request_logs</c> and passes the boolean, so this rule stays unit-testable and the
/// enforcement stays <b>server-side</b> — a stale client that still renders the field editable cannot get
/// around it (AC-S4-02).</para>
/// </summary>
public sealed class ChannelIdLockGuard
{
    /// <summary>
    /// True when the channel's ID may no longer change — either the persisted lock flag is set, or the
    /// caller's probe found a logged 2xx for this channel.
    /// </summary>
    public bool IsLocked(ServiceChannel channel, bool hasLoggedSuccessfulRequest)
    {
        ArgumentNullException.ThrowIfNull(channel);

        return channel.ChannelIdLocked || hasLoggedSuccessfulRequest;
    }

    /// <summary>
    /// Validates an attempted ID change against the lock.
    ///
    /// <para>A <c>null</c> <paramref name="requestedChannelId"/> means the client did not submit the field
    /// (which a locked channel's read-only form does) — not a change, so it is valid. A submitted value
    /// equal to the persisted one is likewise no change, which is what lets a rename (BR-06) or a status
    /// toggle still save on a locked channel. Because VR-F04 matches the ID in the URL <b>exactly as
    /// entered</b>, a case-only difference is a real change and is rejected.</para>
    /// </summary>
    public ChannelValidationResult ValidateIdChange(
        ServiceChannel channel,
        bool hasLoggedSuccessfulRequest,
        string? requestedChannelId)
    {
        ArgumentNullException.ThrowIfNull(channel);

        if (requestedChannelId is null || string.Equals(requestedChannelId, channel.ChannelId, StringComparison.Ordinal))
        {
            return ChannelValidationResult.Valid;
        }

        return IsLocked(channel, hasLoggedSuccessfulRequest)
            ? ChannelValidationResult.Invalid(new ChannelValidationError(
                ChannelErrorCodes.ChannelIdLocked,
                "The service channel ID is locked after the channel's first successful request and can no longer be changed",
                ChannelFields.ChannelId))
            : ChannelValidationResult.Valid;
    }
}
