namespace Nabadat.IntegrationHub.Application.Channels.Dtos;

/// <summary>
/// A service channel as the console reads it — SCR-03's table row (FR-S3-01) and SCR-04's edit form
/// (FR-S4-01/02) in one projection.
///
/// <para><see cref="ChannelIdLocked"/> is what SCR-04 renders the ID field read-only from (AC-S4-02); the
/// three counts are what SCR-03's table columns show. <see cref="Contract"/> is populated on the
/// single-channel read and left empty on list reads, so listing 100 channels does not fan out into 100
/// contract queries.</para>
/// </summary>
/// <param name="Id">Surrogate key.</param>
/// <param name="NameEn">Channel name · EN.</param>
/// <param name="NameAr">Channel name · AR.</param>
/// <param name="ChannelId">The inbound path segment, exactly as stored (VR-F04).</param>
/// <param name="Description">Optional free text.</param>
/// <param name="Active">Status (BR-07 — there is no deleted state).</param>
/// <param name="ChannelIdLocked">True once the channel's first 2xx request landed (BR-05).</param>
/// <param name="SupportedCount">Contract rows with Supported on.</param>
/// <param name="RequiredCount">Contract rows with Required on.</param>
/// <param name="IntegrationsCount">Integrations attached to this channel.</param>
/// <param name="Contract">The contract rows — populated on a single-channel read only.</param>
/// <param name="CreatedAt">Creation instant (UTC).</param>
/// <param name="UpdatedAt">Last-change instant (UTC).</param>
public sealed record ServiceChannelDto(
    Guid Id,
    string NameEn,
    string NameAr,
    string ChannelId,
    string? Description,
    bool Active,
    bool ChannelIdLocked,
    int SupportedCount,
    int RequiredCount,
    int IntegrationsCount,
    IReadOnlyList<ChannelContractRowDto> Contract,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
