using FluentAssertions;
using Nabadat.IntegrationHub.Application.Channels;
using Nabadat.IntegrationHub.Domain.Entities;
using Xunit;

namespace Nabadat.IntegrationHub.UnitTests.Channels;

/// <summary>
/// T024 [US1] — unit tests for <c>ChannelIdLockGuard</c> (BR-05 / FR-S4-02 / AC-S4-02): the channel ID is
/// editable until the channel's first successful (2xx) request, then locked permanently. The lock is
/// enforced <b>server-side</b>, so a stale client that still renders the field editable cannot change it
/// (contracts/api-endpoints.md: 409 <c>channel.id_locked</c>).
///
/// <para>Contract these tests pin for the implementer (T031):
/// <list type="bullet">
///   <item><c>ChannelIdLockGuard</c> in <c>Application/Channels/</c> with two pure methods:
///   <c>bool IsLocked(ServiceChannel channel, bool hasLoggedSuccessfulRequest)</c> and
///   <c>ChannelValidationResult ValidateIdChange(ServiceChannel channel, bool hasLoggedSuccessfulRequest,
///   string? requestedChannelId)</c>.</item>
///   <item>Two independent lock sources, OR-ed: the persisted one-way
///   <see cref="ServiceChannel.ChannelIdLocked"/> flag (set by the first 2xx in US4) <b>and</b> a live
///   "has this channel logged a 2xx?" probe the caller passes in. Either alone locks the ID — the probe is
///   defence in depth for the case where traffic exists but the flag was never written.</item>
///   <item>A locked channel receiving the <i>same</i> ID is valid: only an actual change is rejected, so a
///   client that round-trips the unchanged field can still save a rename or a status toggle (BR-06).</item>
///   <item>Because VR-F04 stores and matches the ID <b>exactly as entered</b>, a case-only difference is a
///   real change and is rejected on a locked channel.</item>
/// </list>
/// This type never queries the log table itself — <c>ServiceChannelService</c> (T034) resolves
/// <c>hasLoggedSuccessfulRequest</c> and passes it in, keeping the guard pure and unit-testable.</para>
/// </summary>
public sealed class ChannelIdLockGuardTests
{
    private static readonly ChannelIdLockGuard Guard = new();

    private static ServiceChannel Channel(string channelId = "KIOSK-01", bool locked = false) => new()
    {
        Id = Guid.NewGuid(),
        NameEn = "Self-Service Kiosk",
        NameAr = "كشك الخدمة الذاتية",
        ChannelId = channelId,
        Active = true,
        ChannelIdLocked = locked,
    };

    [Fact]
    public void IsLocked_returns_true_when_the_channel_has_logged_a_successful_request()
    {
        // The normative spec.md required case: IsLocked(channel, hasLoggedSuccessfulRequest=true) → true.
        Guard.IsLocked(Channel(), hasLoggedSuccessfulRequest: true).Should().BeTrue();
    }

    [Fact]
    public void IsLocked_returns_false_when_the_channel_has_no_successful_request_yet()
    {
        // The normative spec.md required case: IsLocked(channel, hasLoggedSuccessfulRequest=false) → false;
        // the channelId is still editable and a pre-lock edit changes the endpoint path (BR-05).
        Guard.IsLocked(Channel(), hasLoggedSuccessfulRequest: false).Should().BeFalse();
    }

    [Fact]
    public void IsLocked_returns_true_when_the_persisted_lock_flag_is_set_even_without_a_live_probe_hit()
    {
        Guard.IsLocked(Channel(locked: true), hasLoggedSuccessfulRequest: false).Should().BeTrue();
    }

    [Fact]
    public void ValidateIdChange_returns_invalid_id_locked_when_a_locked_channel_receives_a_different_id()
    {
        // "a subsequent PUT changing channelId → rejected server-side" (spec.md required case).
        var result = Guard.ValidateIdChange(
            Channel("KIOSK-01", locked: true), hasLoggedSuccessfulRequest: true, requestedChannelId: "KIOSK-02");

        result.IsValid.Should().BeFalse();
        result.HasCode(ChannelErrorCodes.ChannelIdLocked).Should().BeTrue();
    }

    [Fact]
    public void ValidateIdChange_returns_invalid_id_locked_when_traffic_exists_but_the_flag_was_never_written()
    {
        var result = Guard.ValidateIdChange(
            Channel("KIOSK-01", locked: false), hasLoggedSuccessfulRequest: true, requestedChannelId: "KIOSK-02");

        result.IsValid.Should().BeFalse();
        result.HasCode(ChannelErrorCodes.ChannelIdLocked).Should().BeTrue();
    }

    [Fact]
    public void ValidateIdChange_returns_invalid_id_locked_when_a_locked_channel_receives_a_case_only_change()
    {
        // VR-F04 matches the ID in the URL exactly as entered, so re-casing it IS a change of the endpoint.
        var result = Guard.ValidateIdChange(
            Channel("KIOSK-01", locked: true), hasLoggedSuccessfulRequest: true, requestedChannelId: "kiosk-01");

        result.IsValid.Should().BeFalse();
        result.HasCode(ChannelErrorCodes.ChannelIdLocked).Should().BeTrue();
    }

    [Fact]
    public void ValidateIdChange_returns_valid_when_a_locked_channel_receives_the_unchanged_id()
    {
        // Renaming EN/AR or toggling status must still succeed on a locked channel (BR-06).
        Guard.ValidateIdChange(
                Channel("KIOSK-01", locked: true), hasLoggedSuccessfulRequest: true, requestedChannelId: "KIOSK-01")
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateIdChange_returns_valid_when_the_channel_is_not_locked()
    {
        Guard.ValidateIdChange(
                Channel("KIOSK-01"), hasLoggedSuccessfulRequest: false, requestedChannelId: "KIOSK-02")
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateIdChange_returns_valid_when_a_locked_channel_receives_no_id_at_all()
    {
        // A client that omits the field is not attempting a change — the persisted ID simply stands.
        Guard.ValidateIdChange(
                Channel("KIOSK-01", locked: true), hasLoggedSuccessfulRequest: true, requestedChannelId: null)
            .IsValid.Should().BeTrue();
    }
}
