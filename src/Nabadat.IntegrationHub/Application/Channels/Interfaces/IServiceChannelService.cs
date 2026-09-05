using Nabadat.IntegrationHub.Application.Channels.Dtos;

namespace Nabadat.IntegrationHub.Application.Channels.Interfaces;

/// <summary>
/// The service-channel aggregate's write and read surface (US1), consumed by
/// <c>ServiceChannelsController</c> and — from US3 onward — by the integration-provisioning path that needs
/// the active-channels list.
///
/// <para><b>There is deliberately no delete operation</b> (BR-07 / FR-S3-02): a channel is deactivated, never
/// removed, and one that has ever received traffic can never be removed at all. Adding a delete here would
/// be a spec violation, not a missing feature.</para>
///
/// <para>This is the unit-test mock seam for the aggregate (CLAUDE.md Unit Test Policy) and the interface the
/// composition root binds in <c>IntegrationHubServiceCollectionExtensions</c>.</para>
/// </summary>
public interface IServiceChannelService
{
    /// <summary>
    /// Creates a channel and its parameter contract in one transaction, together with the
    /// <c>channel.created</c> M-17 audit row (DB-08: the change and its audit commit or roll back together).
    /// Validation failures come back on the result, not as exceptions.
    /// </summary>
    Task<ServiceChannelSaveResult> CreateAsync(ServiceChannelCreateCommand command, CancellationToken ct = default);

    /// <summary>
    /// Updates a channel, replacing its contract wholesale, and appends the <c>channel.updated</c> audit row
    /// plus <c>channel.id_changed</c> / <c>channel.activated</c> / <c>channel.deactivated</c> where those
    /// transitions occurred. A post-lock channel-ID change is rejected with <c>channel.id_locked</c> (BR-05).
    /// </summary>
    Task<ServiceChannelSaveResult> UpdateAsync(Guid id, ServiceChannelUpdateCommand command, CancellationToken ct = default);

    /// <summary>
    /// Returns one cursor page of channels with SCR-03's supported/required/integration counts
    /// (contract rows are not projected here — see <see cref="GetAsync"/>).
    /// </summary>
    Task<ServiceChannelPage> ListAsync(string? cursor = null, int limit = 50, CancellationToken ct = default);

    /// <summary>Returns one channel including its full parameter contract, or <c>null</c> when absent.</summary>
    Task<ServiceChannelDto?> GetAsync(Guid id, CancellationToken ct = default);
}
